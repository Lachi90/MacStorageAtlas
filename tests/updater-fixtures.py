#!/usr/bin/env python3
import argparse
import base64
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys
import time
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / 'packaging/.cache/sparkle/2.9.6/bin'


def run(arguments, directory, log):
    with log.open('ab') as output:
        subprocess.run([str(value) for value in arguments], cwd=directory,
                       stdout=output, stderr=subprocess.STDOUT, check=True)


def manifest(directory):
    directory = directory.resolve()
    data = json.loads((directory / 'fixture.json').read_text())
    if data['directory'] != str(directory):
        raise ValueError('The fixture marker does not match its directory.')
    if (directory / 'installed').is_symlink():
        raise ValueError('The fixture installation directory must not be a symlink.')
    return data


def sign_bundle(bundle, identity, log, store=False):
    def sign(path, entitlements=None):
        command = ['codesign', '--force', '--timestamp=none', '--options', 'runtime', '--sign', identity]
        if entitlements:
            command.extend(['--entitlements', entitlements])
        run(command + [path], ROOT, log)
    framework = bundle / 'Contents/Frameworks/Sparkle.framework'
    if framework.exists():
        for component in ['XPCServices/Downloader.xpc', 'XPCServices/Installer.xpc', 'Updater.app', 'Autoupdate']:
            sign(framework / 'Versions/B' / component)
        sign(framework)
    inherited = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.AppStore.Inherit.entitlements'
    for path in sorted((bundle / 'Contents/MacOS').iterdir()):
        if path.is_file() and path.name not in ('UpdateTest', 'AtlasStorageAnalyzer'):
            sign(path, inherited if store else None)
    entitlements = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.entitlements'
    sign(bundle, entitlements)
    run(['codesign', '--verify', '--deep', '--strict', bundle], ROOT, log)


def prepare(directory, architecture, identity, port):
    if directory.exists() and any(directory.iterdir()):
        raise ValueError('Prepare requires an empty fixture directory.')
    directory.mkdir(parents=True, exist_ok=True)
    directory = directory.resolve()
    log = directory / 'prepare.log'
    run([ROOT / 'packaging/prepare-sparkle.sh'], ROOT, log)
    source = directory / 'source'
    source.mkdir()
    shutil.copytree(ROOT / 'src', source / 'src', ignore=shutil.ignore_patterns('bin', 'obj'))
    for name in ['global.json', 'Directory.Build.props']:
        shutil.copy2(ROOT / name, source / name)
    app = source / 'src/MacStorageAtlas.App/App.axaml.cs'
    text = app.read_text()
    text = text.replace('new DiskScanner(new MacFileMetadataReader())', 'new UpdateFixtureScanner()')
    text = text.replace('new JsonSettingsService()', 'new JsonSettingsService(' + json.dumps(str(directory / 'user-data/settings.json')) + ')')
    text = text.replace('ScanHistoryStoreLocation.Default()', json.dumps(str(directory / 'user-data/history')))
    text = text.replace('Dispatcher.UIThread.Post(() => InitializeUpdates(viewModel, desktop));', '''Dispatcher.UIThread.Post(() =>
            {
                if (File.Exists(''' + json.dumps(str(directory / 'busy')) + '''))
                {
                    viewModel.SelectedFolderPath = ''' + json.dumps(str(directory / 'scan-fixture')) + ''';
                    _ = viewModel.ScanFolderCommand.ExecuteAsync(null);
                }
                InitializeUpdates(viewModel, desktop);
            });''')
    app.write_text(text)
    (app.parent / 'UpdateFixtureScanner.cs').write_text('''using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MacStorageAtlas.Core.Scanning;
using MacStorageAtlas.Platform.Mac;
namespace MacStorageAtlas.App;
public sealed class UpdateFixtureScanner : IDiskScanner
{
    public async IAsyncEnumerable<ScanProgress> ScanAsync(string path, ScanOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (File.Exists(''' + json.dumps(str(directory / 'busy')) + '''))
        {
            await Task.Delay(50, cancellationToken);
        }
        await foreach (var progress in new DiskScanner(new MacFileMetadataReader())
            .ScanAsync(path, options, cancellationToken))
        {
            yield return progress;
        }
    }
}
''')
    runtime = 'osx-arm64' if architecture == 'arm64' else 'osx-x64'
    native_arch = 'arm64' if architecture == 'arm64' else 'x86_64'
    payload = directory / 'payload'
    run(['dotnet', 'publish', source / 'src/MacStorageAtlas.App', '-c', 'Release', '-r', runtime,
         '--self-contained', 'true', '-p:DistributionChannel=Direct', '-p:PublishReadyToRun=false', '-o', payload], source, log)
    run(['openssl', 'genpkey', '-algorithm', 'ED25519', '-outform', 'DER', '-out', directory / 'seed.der'], ROOT, log)
    (directory / 'seed.der').chmod(0o600)
    run(['openssl', 'pkey', '-inform', 'DER', '-in', directory / 'seed.der', '-pubout', '-outform', 'DER', '-out', directory / 'public.der'], ROOT, log)
    key = directory / 'private-key'
    key.touch(mode=0o600)
    key.write_text(base64.b64encode((directory / 'seed.der').read_bytes()[-32:]).decode())
    public = base64.b64encode((directory / 'public.der').read_bytes()[-32:]).decode()
    identifier = 'de.ltsoftware.macstorageatlas.update-tests.' + architecture
    name = 'Atlas Update Test ' + architecture
    for version in ['1.0.0', '1.0.1']:
        bundle = directory / version / (name + '.app')
        macos = bundle / 'Contents/MacOS'
        shutil.copytree(payload, macos)
        for path in macos.glob('*.pdb'):
            path.unlink()
        (macos / 'MacStorageAtlas.App').unlink(missing_ok=True)
        framework = bundle / 'Contents/Frameworks/Sparkle.framework'
        shutil.copytree(TOOLS.parent / 'Sparkle.framework', framework, symlinks=True)
        run(['clang', '-arch', native_arch, '-mmacosx-version-min=11.0', '-O2',
             '-DMACSTORAGEATLAS_MANAGED_ASSEMBLY="MacStorageAtlas.App.dll"', '-framework', 'CoreFoundation',
             '-o', macos / 'UpdateTest', ROOT / 'packaging/native-host.c'], ROOT, log)
        run(['clang', '-arch', native_arch, '-mmacosx-version-min=11.0', '-fobjc-arc', '-dynamiclib',
             '-DMACSTORAGEATLAS_UPDATER_TESTING', '-F', TOOLS.parent, '-framework', 'Cocoa', '-framework', 'Sparkle',
             '-Wl,-rpath,@loader_path/../Frameworks', '-install_name', '@rpath/libMacStorageAtlasUpdater.dylib',
             '-o', macos / 'libMacStorageAtlasUpdater.dylib', ROOT / 'packaging/updater-bridge.m'], ROOT, log)
        metadata = dict(CFBundleName=name, CFBundleDisplayName=name, CFBundleIdentifier=identifier,
                        CFBundleVersion=version, CFBundleShortVersionString=version, CFBundleExecutable='UpdateTest',
                        CFBundlePackageType='APPL', LSMinimumSystemVersion='11.0', NSHighResolutionCapable=True,
                        SUFeedURL=f'http://127.0.0.1:{port}/appcast.xml', SUPublicEDKey=public,
                        SURequireSignedFeed=True, SUVerifyUpdateBeforeExtraction=True,
                        SUEnableAutomaticChecks=False, SUAllowsAutomaticUpdates=False,
                        SUScheduledCheckInterval=86400, SUSendProfileInfo=False,
                        NSAppTransportSecurity=dict(NSAllowsLocalNetworking=True))
        with (bundle / 'Contents/Info.plist').open('wb') as output:
            plistlib.dump(metadata, output)
        sign_bundle(bundle, identity, log)
    feed = directory / 'feed'
    feed.mkdir()
    run(['ditto', '--rsrc', '--keepParent', '-c', '-k', directory / '1.0.1' / (name + '.app'), feed / 'update.zip'], ROOT, log)
    run([TOOLS / 'generate_appcast', '--ed-key-file', key, '--maximum-deltas', '0',
         '--download-url-prefix', f'http://127.0.0.1:{port}/', '--disable-signing-warning', feed], ROOT, log)
    shutil.copy2(feed / 'appcast.xml', directory / 'base-appcast.xml')
    scan = directory / 'scan-fixture'
    scan.mkdir()
    (scan / 'example.txt').write_text('Isolated update acceptance fixture.\n')
    (directory / 'fixture.json').write_text(json.dumps(dict(directory=str(directory), architecture=architecture,
        identifier=identifier, name=name, port=port, identity=identity), indent=2))
    reset(directory)
    scenario(directory, 'valid')
    print(directory)


def reset(directory):
    data = manifest(directory)
    installation = directory / 'installed'
    if data['name'] not in ('Atlas Update Test arm64', 'Atlas Update Test x64') or installation.is_symlink():
        raise ValueError('Reset requires an owned fixture app and a regular installation directory.')
    installation.mkdir(exist_ok=True)
    subprocess.run(['chflags', 'nouchg', installation], check=True)
    installation.chmod(0o755)
    bundle = installation / (data['name'] + '.app')
    running = subprocess.run(['pgrep', '-f', str(bundle / 'Contents/MacOS/UpdateTest')], stdout=subprocess.DEVNULL)
    if running.returncode == 0:
        raise ValueError('Quit the isolated test app before resetting it.')
    if bundle.exists():
        shutil.rmtree(bundle)
    run(['ditto', directory / '1.0.0' / bundle.name, bundle], ROOT, directory / 'prepare.log')
    run(['codesign', '--verify', '--deep', '--strict', bundle], ROOT, directory / 'prepare.log')
    print('Installed isolated version 1.0.0:', bundle)


def scenario(directory, mode):
    manifest(directory)
    raw = (directory / 'base-appcast.xml').read_bytes()
    if mode == 'tampered':
        raw = raw.replace(b'<sparkle:version>1.0.1</sparkle:version>', b'<sparkle:version>9.0.0</sparkle:version>')
        if raw == (directory / 'base-appcast.xml').read_bytes():
            raise ValueError('Could not alter the signed fixture version.')
    if mode == 'malformed':
        raw = b'This fixture is not an XML appcast.'
    (directory / 'feed/appcast.xml').write_bytes(raw)
    if mode == 'malformed':
        run([TOOLS / 'sign_update', '--ed-key-file', directory / 'private-key', '--disable-signing-warning',
             directory / 'feed/appcast.xml'], ROOT, directory / 'prepare.log')
    (directory / 'scenario.json').write_text(json.dumps(dict(mode=mode)))
    print('Scenario:', mode)


def serve(directory):
    data = manifest(directory)
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            mode = json.loads((directory / 'scenario.json').read_text())['mode']
            path = urlsplit(self.path).path
            print(json.dumps(dict(event='request', path=path, query=urlsplit(self.path).query,
                                  user_agent=self.headers.get('User-Agent'), mode=mode)), flush=True)
            if path == '/appcast.xml' and mode == 'http-error':
                self.send_error(503, 'Isolated fixture failure')
                return
            name = {'/appcast.xml': 'appcast.xml', '/update.zip': 'update.zip'}.get(path)
            if name is None:
                self.send_error(404)
                return
            source = directory / 'feed' / name
            self.send_response(200)
            self.send_header('Content-Length', str(source.stat().st_size))
            self.send_header('Content-Type', 'application/xml' if name.endswith('.xml') else 'application/zip')
            self.end_headers()
            sent = 0
            try:
                with source.open('rb') as stream:
                    while block := stream.read(65536):
                        self.wfile.write(block)
                        self.wfile.flush()
                        sent += len(block)
                        if mode == 'slow' and name.endswith('.zip'):
                            time.sleep(0.25)
                print(json.dumps(dict(event='complete', path=path, bytes=sent)), flush=True)
            except (BrokenPipeError, ConnectionResetError):
                print(json.dumps(dict(event='cancelled', path=path, bytes=sent)), flush=True)
        def log_message(self, format, *args):
            pass
    print('Loopback fixture server:', data['port'], flush=True)
    ThreadingHTTPServer(('127.0.0.1', data['port']), Handler).serve_forever()


def preferences(directory, background):
    data = manifest(directory)
    subprocess.run(['defaults', 'write', data['identifier'], 'SUEnableAutomaticChecks', '-bool', str(background).lower()], check=True)
    if background:
        subprocess.run(['defaults', 'write', data['identifier'], 'SUHasLaunchedBefore', '-bool', 'true'], check=True)
        subprocess.run(['defaults', 'write', data['identifier'], 'SULastCheckTime', '-date', '2000-01-01 00:00:00 +0000'], check=True)
    print('Fixture automatic checks:', background)


def prepare_store(directory, original, identity):
    manifest(directory)
    if not (original / 'Contents/embedded.provisionprofile').exists():
        raise ValueError('Store acceptance requires an existing provisioned Store bundle.')
    source = directory / 'source'
    app = source / 'src/MacStorageAtlas.App/App.axaml.cs'
    text = app.read_text()
    text = text.replace(json.dumps(str(directory / 'user-data/settings.json')),
        'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacStorageAtlasUpdateAcceptance", "settings.json")')
    text = text.replace(json.dumps(str(directory / 'user-data/history')),
        'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacStorageAtlasUpdateAcceptance", "history")')
    app.write_text(text)
    log = directory / 'store-prepare.log'
    bundle = directory / 'store' / 'AtlasStorageAnalyzer.app'
    if bundle.exists():
        raise ValueError('The isolated Store bundle already exists.')
    run(['ditto', original, bundle], ROOT, log)
    signed = subprocess.run(['codesign', '--display', '--entitlements', ':-', original],
        check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    entitlements = directory / 'store-entitlements.plist'
    entitlements.write_bytes(plistlib.dumps(plistlib.loads(signed.stdout)))
    inherited = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.AppStore.Inherit.entitlements'
    slice_entitlements = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.AppStore.Slice.entitlements'
    def sign(path, permissions):
        run(['codesign', '--force', '--timestamp=none', '--options', 'runtime', '--entitlements', permissions,
             '--sign', identity, path], ROOT, log)
    slice_bundle = bundle / 'Contents/Helpers/AtlasStorageAnalyzer-x86_64.app'
    for runtime, target in [('osx-arm64', bundle), ('osx-x64', slice_bundle)]:
        payload = directory / ('store-payload-' + runtime)
        run(['dotnet', 'publish', source / 'src/MacStorageAtlas.App', '-c', 'Release', '-r', runtime,
             '--self-contained', 'true', '-p:DistributionChannel=AppStore', '-p:PublishReadyToRun=false', '-o', payload], source, log)
        (payload / 'MacStorageAtlas.App').unlink(missing_ok=True)
        for path in payload.glob('*.pdb'):
            path.unlink()
        run(['ditto', payload, target / 'Contents/MacOS'], ROOT, log)
        for path in sorted((target / 'Contents/MacOS').iterdir()):
            if path.is_file() and path.name != 'AtlasStorageAnalyzer':
                sign(path, inherited)
        if target == slice_bundle:
            sign(target / 'Contents/MacOS/AtlasStorageAnalyzer', slice_entitlements)
            sign(target, slice_entitlements)
    sign(bundle / 'Contents/MacOS/AtlasStorageAnalyzer', entitlements)
    sign(bundle, entitlements)
    run(['codesign', '--verify', '--deep', '--strict', bundle], ROOT, log)
    run([ROOT / 'packaging/verify-no-updater.sh', bundle], ROOT, log)
    print('Isolated sandbox Store fixture:', bundle)


def prepare_local_sandbox(directory, identity):
    manifest(directory)
    original = directory / 'store/AtlasStorageAnalyzer.app'
    bundle = directory / 'store-local/AtlasStorageAnalyzer.app'
    if bundle.exists():
        raise ValueError('The local sandbox fixture already exists.')
    log = directory / 'store-local-prepare.log'
    run(['ditto', original, bundle], ROOT, log)
    slice_bundle = bundle / 'Contents/Helpers/AtlasStorageAnalyzer-x86_64.app'
    for target, identifier in [(bundle, 'de.ltsoftware.macstorageatlas.update-tests.store-local'),
                               (slice_bundle, 'de.ltsoftware.macstorageatlas.update-tests.store-local.x86-64')]:
        metadata_path = target / 'Contents/Info.plist'
        with metadata_path.open('rb') as stream:
            metadata = plistlib.load(stream)
        metadata['CFBundleIdentifier'] = identifier
        with metadata_path.open('wb') as stream:
            plistlib.dump(metadata, stream)
        (target / 'Contents/embedded.provisionprofile').unlink(missing_ok=True)
        inherited = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.AppStore.Inherit.entitlements'
        for path in sorted((target / 'Contents/MacOS').iterdir()):
            if path.is_file() and path.name != 'AtlasStorageAnalyzer':
                run(['codesign', '--force', '--timestamp=none', '--options', 'runtime', '--entitlements', inherited,
                     '--sign', identity, path], ROOT, log)
        if target == slice_bundle:
            permissions = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.AppStore.Slice.entitlements'
            for path in [target / 'Contents/MacOS/AtlasStorageAnalyzer', target]:
                run(['codesign', '--force', '--timestamp=none', '--options', 'runtime', '--entitlements', permissions,
                     '--sign', identity, path], ROOT, log)
    permissions = ROOT / 'src/MacStorageAtlas.App/MacStorageAtlas.AppStore.entitlements'
    for path in [bundle / 'Contents/MacOS/AtlasStorageAnalyzer', bundle]:
        run(['codesign', '--force', '--timestamp=none', '--options', 'runtime', '--entitlements', permissions,
             '--sign', identity, path], ROOT, log)
    run(['codesign', '--verify', '--deep', '--strict', bundle], ROOT, log)
    run([ROOT / 'packaging/verify-no-updater.sh', bundle], ROOT, log)
    print('Developer ID sandbox payload smoke fixture:', bundle)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['prepare', 'serve', 'scenario', 'reset', 'background-on', 'background-off', 'deny', 'allow', 'busy', 'idle', 'store', 'store-local'])
    parser.add_argument('directory', type=Path)
    parser.add_argument('--architecture', choices=['arm64', 'x64'], default='arm64')
    parser.add_argument('--identity')
    parser.add_argument('--store-bundle', type=Path)
    parser.add_argument('--port', type=int, default=8781)
    parser.add_argument('--mode', choices=['valid', 'slow', 'malformed', 'tampered', 'http-error'], default='valid')
    arguments = parser.parse_args()
    directory = arguments.directory.resolve()
    if arguments.action == 'prepare':
        if not arguments.identity:
            parser.error('prepare requires --identity')
        prepare(directory, arguments.architecture, arguments.identity, arguments.port)
        return
    manifest(directory)
    if arguments.action == 'store-local':
        if not arguments.identity:
            parser.error('store-local requires --identity')
        prepare_local_sandbox(directory, arguments.identity)
    elif arguments.action == 'store':
        if not arguments.identity or not arguments.store_bundle:
            parser.error('store requires --identity and --store-bundle')
        prepare_store(directory, arguments.store_bundle.resolve(), arguments.identity)
    elif arguments.action == 'serve':
        serve(directory)
    elif arguments.action == 'scenario':
        scenario(directory, arguments.mode)
    elif arguments.action == 'reset':
        reset(directory)
    elif arguments.action.startswith('background-'):
        preferences(directory, arguments.action == 'background-on')
    elif arguments.action in ['deny', 'allow']:
        installation = directory / 'installed'
        subprocess.run(['chflags', 'nouchg', installation], check=True)
        installation.chmod(0o555 if arguments.action == 'deny' else 0o755)
        if arguments.action == 'deny':
            subprocess.run(['chflags', 'uchg', installation], check=True)
    elif arguments.action == 'busy':
        (directory / 'busy').touch()
    elif arguments.action == 'idle':
        (directory / 'busy').unlink(missing_ok=True)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        sys.exit(str(error))

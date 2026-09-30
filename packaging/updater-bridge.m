#import <Cocoa/Cocoa.h>
#import <Sparkle/Sparkle.h>

typedef int (*MSARestartGate)(void);
typedef void (*MSAStateChanged)(void);
typedef int (*MSAHasResults)(void);

static NSString *const MSAApprovedInstallationVersionKey = @"MSAApprovedInstallationVersion";

static NSString *MSAConsumeIncompleteInstallation(NSUserDefaults *defaults, NSString *installedVersion)
{
    NSString *expectedVersion = [defaults stringForKey:MSAApprovedInstallationVersionKey];
    [defaults removeObjectForKey:MSAApprovedInstallationVersionKey];
    if (expectedVersion.length == 0 || installedVersion.length == 0) {
        return nil;
    }
    return [[SUStandardVersionComparator defaultComparator] compareVersion:expectedVersion toVersion:installedVersion] == NSOrderedDescending
        ? expectedVersion : nil;
}

@interface MSAUpdaterDelegate : NSObject <SPUUpdaterDelegate, SPUStandardUserDriverDelegate>
@property(nonatomic, strong) SPUStandardUpdaterController *controller;
@property(nonatomic, copy) void (^pendingInstallation)(void);
@property(nonatomic) MSARestartGate restartGate;
@property(nonatomic) MSAHasResults hasResults;
@property(nonatomic) MSAStateChanged stateChanged;
@property(nonatomic) BOOL restartRequested;
@end

@implementation MSAUpdaterDelegate
- (void)observeValueForKeyPath:(NSString *)keyPath ofObject:(id)object change:(NSDictionary *)change context:(void *)context
{
    if (self.stateChanged != NULL) {
        self.stateChanged();
    }
}

- (BOOL)updater:(SPUUpdater *)updater shouldPostponeRelaunchForUpdate:(SUAppcastItem *)item untilInvokingBlock:(void (^)(void))installHandler
{
    int decision = self.restartGate != NULL ? self.restartGate() : 0;
    if (decision == 1) {
        return NO;
    }
    self.pendingInstallation = installHandler;
    if (decision == 0) {
        NSAlert *alert = [[NSAlert alloc] init];
        alert.messageText = @"Update restart postponed";
        alert.informativeText = @"Finish the current scan or file operation, export any results you want to keep, then choose Finish Installing Update from the application menu. Restarting closes in-memory scan results; settings and saved history remain available. The app will not restart automatically when the operation finishes.";
        [alert addButtonWithTitle:@"OK"];
        [alert runModal];
    }
    if (self.stateChanged != NULL) {
        self.stateChanged();
    }
    return YES;
}

- (void)standardUserDriverWillHandleShowingUpdate:(BOOL)handleShowingUpdate forUpdate:(SUAppcastItem *)update state:(SPUUserUpdateState *)state
{
    if (handleShowingUpdate && self.hasResults != NULL && self.hasResults() != 0) {
        NSAlert *alert = [[NSAlert alloc] init];
        alert.messageText = @"Installing an update closes the current scan";
        alert.informativeText = @"Export any results you want to keep before installing. Your settings and saved scan history will remain available.";
        [alert addButtonWithTitle:@"OK"];
        [alert runModal];
    }
}

- (NSSet<NSString *> *)allowedChannelsForUpdater:(SPUUpdater *)updater
{
    return [NSSet set];
}

- (BOOL)updater:(SPUUpdater *)updater shouldProceedWithUpdate:(SUAppcastItem *)item updateCheck:(SPUUpdateCheck)updateCheck error:(NSError * __autoreleasing *)error
{
    BOOL requiresArm64 = [item.hardwareRequirements containsObject:@"arm64"];
#if defined(__arm64__)
    BOOL architectureMatches = requiresArm64;
    NSString *runtime = @"osx-arm64";
#else
    BOOL architectureMatches = !requiresArm64;
    NSString *runtime = @"osx-x64";
#endif
    NSRegularExpression *versionPattern = [NSRegularExpression regularExpressionWithPattern:@"^[0-9]+\\.[0-9]+\\.[0-9]+$" options:0 error:NULL];
    BOOL stable = [versionPattern numberOfMatchesInString:item.versionString options:0 range:NSMakeRange(0, item.versionString.length)] == 1;
    BOOL archiveMatches = YES;
#ifndef MACSTORAGEATLAS_UPDATER_TESTING
    NSURL *archive = item.fileURL;
    NSString *expected = [NSString stringWithFormat:@"AtlasStorageAnalyzer-%@-%@.dmg", item.versionString, runtime];
    archiveMatches = [archive.scheme isEqualToString:@"https"] && archive.host.length > 0
        && archive.user == nil && archive.password == nil && [archive.lastPathComponent isEqualToString:expected];
#endif
    if (architectureMatches && stable && archiveMatches) {
        return YES;
    }
    if (error != NULL) {
        *error = [NSError errorWithDomain:@"MacStorageAtlas.Updates" code:1 userInfo:@{NSLocalizedDescriptionKey: @"The update feed does not contain a stable download matching this installed app variant. Retry later or download the correct signed DMG from GitHub Releases."}];
    }
    return NO;
}

- (void)updater:(SPUUpdater *)updater willInstallUpdate:(SUAppcastItem *)item
{
    [NSUserDefaults.standardUserDefaults setObject:item.versionString forKey:MSAApprovedInstallationVersionKey];
    [NSUserDefaults.standardUserDefaults synchronize];
}

- (void)updaterWillRelaunchApplication:(SPUUpdater *)updater
{
    self.restartRequested = YES;
}

- (void)updater:(SPUUpdater *)updater didFinishUpdateCycleForUpdateCheck:(SPUUpdateCheck)updateCheck error:(NSError *)error
{
    if (error != nil) {
        [NSUserDefaults.standardUserDefaults removeObjectForKey:MSAApprovedInstallationVersionKey];
    }
    self.restartRequested = NO;
    self.pendingInstallation = nil;
    if (self.stateChanged != NULL) {
        self.stateChanged();
    }
}

- (NSArray<NSString *> *)allowedSystemProfileKeysForUpdater:(SPUUpdater *)updater
{
    return @[];
}
@end

static MSAUpdaterDelegate *delegate;
static NSString *initializationError;

int msa_updater_start(MSARestartGate restartGate, MSAStateChanged stateChanged, MSAHasResults hasResults)
{
    if (![NSThread isMainThread] || delegate != nil) {
        initializationError = @"The updater must be initialized once on the application thread.";
        return 0;
    }
    NSBundle *bundle = [NSBundle mainBundle];
    NSURL *feed = [NSURL URLWithString:[bundle objectForInfoDictionaryKey:@"SUFeedURL"] ?: @""];
    NSData *key = [[NSData alloc] initWithBase64EncodedString:[bundle objectForInfoDictionaryKey:@"SUPublicEDKey"] ?: @"" options:0];
#ifndef MACSTORAGEATLAS_UPDATER_TESTING
    if (![feed.scheme isEqualToString:@"https"] || feed.host.length == 0 || feed.user != nil || feed.password != nil || key.length != 32) {
        initializationError = @"The update feed or verification key is invalid.";
        return 0;
    }
#endif
    delegate = [[MSAUpdaterDelegate alloc] init];
    delegate.restartGate = restartGate;
    delegate.stateChanged = stateChanged;
    delegate.hasResults = hasResults;
    delegate.controller = [[SPUStandardUpdaterController alloc] initWithStartingUpdater:NO updaterDelegate:delegate userDriverDelegate:delegate];
    SPUUpdater *updater = delegate.controller.updater;
    updater.updateCheckInterval = MAX(updater.updateCheckInterval, 86400.0);
    NSError *error = nil;
    if (![updater startUpdater:&error]) {
        initializationError = error.localizedDescription ?: @"Software updates could not be initialized. Reinstall the latest signed release.";
        delegate = nil;
        return 0;
    }
    [updater addObserver:delegate forKeyPath:@"canCheckForUpdates" options:0 context:NULL];
    [updater addObserver:delegate forKeyPath:@"automaticallyChecksForUpdates" options:0 context:NULL];
    NSString *incompleteVersion = MSAConsumeIncompleteInstallation(NSUserDefaults.standardUserDefaults,
        [bundle objectForInfoDictionaryKey:@"CFBundleVersion"]);
    if (incompleteVersion != nil) {
        dispatch_async(dispatch_get_main_queue(), ^{
            NSAlert *alert = [[NSAlert alloc] init];
            alert.messageText = @"The update installation did not complete";
            alert.informativeText = @"The previous app version is still installed. Your settings and saved history remain available. Check that the app is in a writable location and the app or containing folder is not locked, then choose Check for Updates to retry. You can also install the latest signed DMG from GitHub Releases.";
            [alert addButtonWithTitle:@"OK"];
            [alert runModal];
        });
    }
    return 1;
}

const char *msa_updater_error(void)
{
    return [initializationError UTF8String];
}

int msa_updater_state(void)
{
    if (delegate == nil || ![NSThread isMainThread]) {
        return 0;
    }
    return 1 | (delegate.controller.updater.canCheckForUpdates ? 2 : 0)
        | (delegate.controller.updater.automaticallyChecksForUpdates ? 4 : 0)
        | (delegate.pendingInstallation != nil ? 8 : 0)
        | (delegate.restartRequested ? 16 : 0);
}

void msa_updater_check(void)
{
    if (delegate == nil || ![NSThread isMainThread]) {
        return;
    }
    if (delegate.pendingInstallation != nil) {
        int decision = delegate.restartGate != NULL ? delegate.restartGate() : 0;
        if (decision == 1) {
            void (^handler)(void) = delegate.pendingInstallation;
            delegate.pendingInstallation = nil;
            handler();
        } else if (decision == 0) {
            NSAlert *alert = [[NSAlert alloc] init];
            alert.messageText = @"The app is still busy";
            alert.informativeText = @"Finish the current operation before restarting to install the update.";
            [alert runModal];
        }
    } else if (delegate.controller.updater.canCheckForUpdates) {
        [delegate.controller checkForUpdates:nil];
    }
}

void msa_updater_set_automatic_checks(int enabled)
{
    if (delegate != nil && [NSThread isMainThread]) {
        delegate.controller.updater.automaticallyChecksForUpdates = enabled != 0;
    }
}

#include <CoreFoundation/CoreFoundation.h>
#include <dlfcn.h>
#include <limits.h>
#include <mach-o/dyld.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#ifndef MACSTORAGEATLAS_MANAGED_ASSEMBLY
#error "MACSTORAGEATLAS_MANAGED_ASSEMBLY must be defined with the managed entry assembly file name."
#endif

#define MACSTORAGEATLAS_BUNDLE_DEPTH 3

typedef int (*hostfxr_main_startupinfo_fn)(int argc,
                                           const char *argv[],
                                           const char *host_path,
                                           const char *dotnet_root,
                                           const char *app_path);

static int append_path_component(char *buffer, size_t size, const char *component)
{
    if (strlcat(buffer, "/", size) >= size) {
        return 0;
    }

    if (strlcat(buffer, component, size) >= size) {
        return 0;
    }

    return 1;
}

static int remove_last_path_component(char *buffer)
{
    char *separator = strrchr(buffer, '/');

    if (separator == NULL) {
        return 0;
    }

    *separator = '\0';
    return 1;
}

static int resolve_runtime_directory(const char *host_path, char *buffer, size_t size)
{
    if (strlcpy(buffer, host_path, size) >= size) {
        return 0;
    }

#ifdef MACSTORAGEATLAS_PAYLOAD_PATH
    {
        int level;

        for (level = 0; level < MACSTORAGEATLAS_BUNDLE_DEPTH; level++) {
            if (!remove_last_path_component(buffer)) {
                return 0;
            }
        }
    }

    if (strlcat(buffer, MACSTORAGEATLAS_PAYLOAD_PATH, size) >= size) {
        return 0;
    }
#else
    if (!remove_last_path_component(buffer)) {
        return 0;
    }
#endif

    return 1;
}

int main(int argc, const char *argv[])
{
    char executable_path[PATH_MAX];
    char host_path[PATH_MAX];
    char runtime_directory[PATH_MAX];
    char hostfxr_path[PATH_MAX];
    char managed_assembly_path[PATH_MAX];
    uint32_t path_size = (uint32_t)sizeof(executable_path);
    void *hostfxr = NULL;
    hostfxr_main_startupinfo_fn hostfxr_main_startupinfo = NULL;

    if (CFBundleGetMainBundle() == NULL) {
        return 1;
    }

    if (_NSGetExecutablePath(executable_path, &path_size) != 0) {
        return 1;
    }

    if (realpath(executable_path, host_path) == NULL) {
        return 1;
    }

    if (!resolve_runtime_directory(host_path, runtime_directory, sizeof(runtime_directory))) {
        return 1;
    }

    if (strlcpy(hostfxr_path, runtime_directory, sizeof(hostfxr_path)) >= sizeof(hostfxr_path)) {
        return 1;
    }

    if (!append_path_component(hostfxr_path, sizeof(hostfxr_path), "libhostfxr.dylib")) {
        return 1;
    }

    if (strlcpy(managed_assembly_path, runtime_directory, sizeof(managed_assembly_path)) >= sizeof(managed_assembly_path)) {
        return 1;
    }

    if (!append_path_component(managed_assembly_path, sizeof(managed_assembly_path), MACSTORAGEATLAS_MANAGED_ASSEMBLY)) {
        return 1;
    }

    hostfxr = dlopen(hostfxr_path, RTLD_LAZY | RTLD_LOCAL);
    if (hostfxr == NULL) {
        return 1;
    }

    hostfxr_main_startupinfo = (hostfxr_main_startupinfo_fn)dlsym(hostfxr, "hostfxr_main_startupinfo");
    if (hostfxr_main_startupinfo == NULL) {
        return 1;
    }

    return hostfxr_main_startupinfo(argc, argv, host_path, runtime_directory, managed_assembly_path);
}

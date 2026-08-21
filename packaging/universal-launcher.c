#include <limits.h>
#include <mach-o/dyld.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#ifndef MACSTORAGEATLAS_SLICE_PATH
#error "MACSTORAGEATLAS_SLICE_PATH must be defined with the bundle-relative slice executable path."
#endif

int main(int argc, char *argv[])
{
    char executable_path[PATH_MAX];
    char bundle_path[PATH_MAX];
    char slice_path[PATH_MAX];
    uint32_t path_size = (uint32_t)sizeof(executable_path);
    char *separator = NULL;
    int level = 0;

    (void)argc;

    if (_NSGetExecutablePath(executable_path, &path_size) != 0) {
        return 1;
    }

    if (realpath(executable_path, bundle_path) == NULL) {
        return 1;
    }

    for (level = 0; level < 3; level++) {
        separator = strrchr(bundle_path, '/');
        if (separator == NULL) {
            return 1;
        }

        *separator = '\0';
    }

    if (strlcpy(slice_path, bundle_path, sizeof(slice_path)) >= sizeof(slice_path)) {
        return 1;
    }

    if (strlcat(slice_path, MACSTORAGEATLAS_SLICE_PATH, sizeof(slice_path)) >= sizeof(slice_path)) {
        return 1;
    }

    execv(slice_path, argv);
    return 1;
}

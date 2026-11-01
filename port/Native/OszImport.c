/* Streaming .osz extraction using Mono's bundled zlib-licensed MiniZip. */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <errno.h>
#include <fcntl.h>
#include <unistd.h>
#include <limits.h>
#include <sys/stat.h>
#include <CommonCrypto/CommonDigest.h>
#include "unzip.h"
#include "ImportGate.h"

#define MAX_ENTRIES 10000
#define MAX_EXPANDED (1024ULL * 1024ULL * 1024ULL)
#define MAX_FILE (512ULL * 1024ULL * 1024ULL)

static int fail(char *error, int capacity, const char *message) {
    if (error && capacity > 0) snprintf(error, (size_t)capacity, "%s", message);
    return -1;
}

int legacy_osz_hash(const char *path, char *hex, int capacity) {
    unsigned char buffer[32768], digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256_CTX ctx;
    size_t count;
    FILE *input;
    if (!path || !hex || capacity < 65 || !(input = fopen(path, "rb"))) return -1;
    CC_SHA256_Init(&ctx);
    while ((legacy_import_checkpoint(),count = fread(buffer, 1, sizeof buffer, input)) > 0) {
        if(legacy_import_interrupted()){fclose(input);return -2;}
        CC_SHA256_Update(&ctx, buffer, (CC_LONG)count);
    }
    if (ferror(input)) { fclose(input); return -1; }
    fclose(input);
    CC_SHA256_Final(digest, &ctx);
    for (count = 0; count < sizeof digest; count++) sprintf(hex + count * 2, "%02x", digest[count]);
    hex[64] = 0;
    return 0;
}

static int directory(const char *path) {
    struct stat state;
    if (mkdir(path, 0700) != 0 && errno != EEXIST) return -1;
    if (lstat(path, &state) != 0 || !S_ISDIR(state.st_mode)) return -1;
    return 0;
}

/* Both slash conventions occur in beatmap archives. Reject traversal, links
 * and drive/absolute paths before touching an output file. */
static int normalize(char *name, char *result, size_t capacity) {
    char *part, *cursor = name;
    size_t length = 0;
    for (part = name; *part; part++) if (*part == '\\') *part = '/';
    if (*name == '/' || strchr(name, ':')) return -1;
    while ((part = strsep(&cursor, "/")) != NULL) {
        size_t count = strlen(part);
        if (!count || strcmp(part, ".") == 0) continue;
        if (strcmp(part, "..") == 0 || length + count + 2 > capacity) return -1;
        if (length) result[length++] = '/';
        memcpy(result + length, part, count); length += count;
    }
    result[length] = 0;
    return 0;
}

int legacy_osz_extract(const char *archive, const char *destination, char *error, int capacity) {
    unzFile zip = NULL;
    unz_global_info global;
    unz_file_info info;
    char name[1024], relative[1024], output[PATH_MAX], buffer[32768];
    const char *message = "Invalid or unsupported ZIP archive";
    uint64_t expanded = 0;
    unsigned long index;
    int result = -1, opened = 0;
    FILE *file = NULL;
    struct stat root;
    if (!archive || !destination || lstat(destination, &root) != 0 || !S_ISDIR(root.st_mode))
        return fail(error, capacity, "Import folder is unavailable");
    zip = unzOpen(archive);
    if (!zip) return fail(error, capacity, message);
    if (unzGetGlobalInfo(zip, &global) != UNZ_OK || !global.number_entry || global.number_entry > MAX_ENTRIES) goto done;
    if (unzGoToFirstFile(zip) != UNZ_OK) goto done;
    for (index = 0; index < global.number_entry; index++) {
        legacy_import_checkpoint();
        if(legacy_import_interrupted()){result=-2;goto done;}
        uint64_t written = 0;
        size_t length;
        int is_directory, status;
        unsigned long kind;
        if (unzGetCurrentFileInfo(zip, &info, NULL, 0, NULL, 0, NULL, 0) != UNZ_OK) goto done;
        if (!info.size_filename || info.size_filename >= sizeof name) { message = "An archive filename is too long"; goto done; }
        memset(name, 0, sizeof name);
        if (unzGetCurrentFileInfo(zip, &info, name, sizeof name, NULL, 0, NULL, 0) != UNZ_OK) goto done;
        if (strlen(name) != info.size_filename) { message = "Invalid archive filename"; goto done; }
        length = strlen(name);
        is_directory = name[length-1] == '/' || name[length-1] == '\\';
        kind = (info.external_fa >> 16) & 0170000;
        if (kind && kind != 0100000 && kind != 0040000) { message = "Archive links and special files are unsupported"; goto done; }
        if (kind == 0040000) is_directory = 1;
        if (normalize(name, relative, sizeof relative) != 0) { message = "Archive contains an unsafe path"; goto done; }
        if (!relative[0]) {
            if (is_directory) goto next_entry;
            message = "Invalid empty archive filename"; goto done;
        }
        if (info.flag & 1) { message = "Password-protected archives are unsupported"; goto done; }
        if (info.uncompressed_size > MAX_FILE || expanded + info.uncompressed_size > MAX_EXPANDED) { message = "Archive exceeds the 1 GB import limit (512 MB per file)"; goto done; }
        if (snprintf(output, sizeof output, "%s/%s", destination, relative) >= (int)sizeof output) { message = "An import path is too long"; goto done; }
        {
            char *p;
            for (p = output + strlen(destination) + 1; *p; p++) if (*p == '/') {
                *p = 0;
                status = directory(output);
                *p = '/';
                if (status != 0) { message = "Could not create an import subfolder"; goto done; }
            }
        }
        if (is_directory) {
            if (directory(output) != 0) { message = "Could not create an import folder"; goto done; }
        } else {
            int fd;
            if (unzOpenCurrentFile(zip) != UNZ_OK) goto done;
            opened = 1;
            fd = open(output, O_CREAT | O_EXCL | O_WRONLY | O_NOFOLLOW, 0600);
            if (fd < 0) { message = "Could not create a file (duplicate names or storage full)"; goto done; }
            file = fdopen(fd, "wb");
            if (!file) { close(fd); message = "Could not write an import file"; goto done; }
            while ((legacy_import_checkpoint(),status = unzReadCurrentFile(zip, buffer, sizeof buffer)) > 0) {
                if(legacy_import_interrupted()){result=-2;goto done;}
                written += (unsigned)status;
                if (written > info.uncompressed_size || expanded + written > MAX_EXPANDED) { message = "Archive expands beyond its declared size"; goto done; }
                if (fwrite(buffer, 1, (size_t)status, file) != (size_t)status) { message = "Not enough storage to import this archive"; goto done; }
            }
            if (fclose(file) != 0) { file = NULL; message = "Could not finish writing an import file"; goto done; }
            file = NULL;
            if (status < 0 || written != info.uncompressed_size) { message = "Archive data is truncated or corrupt"; goto done; }
            status = unzCloseCurrentFile(zip); opened = 0;
            if (status != UNZ_OK) { message = "Archive checksum failed"; goto done; }
            expanded += written;
        }
next_entry:
        if (index + 1 < global.number_entry && unzGoToNextFile(zip) != UNZ_OK) goto done;
    }
    result = 0;
    fprintf(stderr, "OSU archive: extracted %lu entries, %llu bytes\n", global.number_entry, (unsigned long long)expanded);
done:
    if (file) fclose(file);
    if (opened) unzCloseCurrentFile(zip);
    unzClose(zip);
    if(result==-2)return -2;
    if (result != 0) { fprintf(stderr, "OSU archive: %s\n", message); return fail(error, capacity, message); }
    if (error && capacity > 0) error[0] = 0;
    return 0;
}

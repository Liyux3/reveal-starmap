// Workshop release tool for Reveal StarMap; never shipped in the mod.
//
// ONI installs a Workshop mod only when GetItemInstallInfo returns a regular ZIP file
// (KMod.Steam.MakeMod). A modern UGC content update turns the item into a depot directory that
// ONI cannot install, so this tool uses only the legacy RemoteStorage contract of the official
// ONI Mod Uploader (creator app 636750) and changes nothing but the file and its change note.
// Title, description, tags, preview and visibility stay as they are.
//
//   workshop-publish SDK publish OWNER ZIP NOTE_FILE RECEIPT
//   workshop-publish SDK verify  OWNER ZIP MIN_TIMESTAMP RECEIPT
//
// publish  stages ZIP in Steam Cloud and commits it to the item with the change note.
// verify   downloads the item as the game (app 457140) and requires a regular file install,
//          updated at or after MIN_TIMESTAMP, whose bytes equal ZIP.
#include <dlfcn.h>
#include <inttypes.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <time.h>
#include <unistd.h>

static const uint32_t GAME_APP = 457140;
static const uint32_t CREATOR_APP = 636750;
static const uint64_t ITEM = UINT64_C(3695988693);
enum { UPDATE_RESULT_CALLBACK = 1316, STATE_LEGACY = 2, STATE_INSTALLED = 4, STATE_BUSY = 8 | 16 | 32 };

#pragma pack(push, 4)
typedef struct { int32_t result; uint64_t item; bool legal; } UpdateResult;
#pragma pack(pop)
_Static_assert(sizeof(UpdateResult) == 16 && offsetof(UpdateResult, item) == 4 &&
    offsetof(UpdateResult, legal) == 12, "RemoteStorageUpdatePublishedFileResult_t layout");

static void *library;

static void fail(const char *message) {
    fprintf(stderr, "workshop-publish: %s\n", message);
    exit(1);
}

static void *symbol(const char *name) {
    void *value = dlsym(library, name);
    if (!value) fail(name);
    return value;
}
#define LOAD(ret, name, args) ret (*name) args = (ret (*) args)symbol(#name)

// Receipts are flushed before and after each remote write, so an interrupted run is never retried blind.
static void receipt(const char *path, const char *format, ...) {
    FILE *file = fopen(path, "w");
    if (!file) fail("cannot write receipt");
    va_list args;
    va_start(args, format);
    vfprintf(file, format, args);
    va_end(args);
    fputc('\n', file);
    if (fflush(file) || fsync(fileno(file))) fail("cannot flush receipt");
    fclose(file);
}

static unsigned char *read_file(const char *path, size_t limit, size_t *size) {
    struct stat info;
    if (stat(path, &info) || !S_ISREG(info.st_mode) || info.st_size <= 0 || (size_t)info.st_size > limit)
        return NULL;
    FILE *file = fopen(path, "rb");
    unsigned char *bytes = malloc((size_t)info.st_size + 1);
    if (!file || !bytes || fread(bytes, 1, (size_t)info.st_size, file) != (size_t)info.st_size) fail(path);
    fclose(file);
    bytes[info.st_size] = 0;
    *size = (size_t)info.st_size;
    return bytes;
}

int main(int argc, char **argv) {
    if (argc != 7 || (strcmp(argv[2], "publish") && strcmp(argv[2], "verify")))
        fail("usage: workshop-publish SDK publish|verify OWNER ZIP NOTE_FILE|MIN_TIMESTAMP RECEIPT");
    bool publish = !strcmp(argv[2], "publish");
    char *end;
    uint64_t owner = strtoull(argv[3], &end, 10);
    if (!owner || *end) fail("OWNER must be the item owner's SteamID64");
    if (!access(argv[6], F_OK)) fail("RECEIPT exists; reconcile it before another run");

    size_t zip_size = 0;
    unsigned char *zip = read_file(argv[4], 10 * 1024 * 1024, &zip_size);
    if (!zip || zip_size < 22 || memcmp(zip, "PK\x03\x04", 4)) fail("ZIP must be one ZIP file under 10 MB");

    uint32_t app = publish ? CREATOR_APP : GAME_APP;
    char app_text[16];
    snprintf(app_text, sizeof(app_text), "%u", app);
    setenv("SteamAppId", app_text, 1);
    setenv("SteamGameId", app_text, 1);
    library = dlopen(argv[1], RTLD_NOW | RTLD_LOCAL);
    if (!library) fail(dlerror());

    LOAD(bool, SteamAPI_Init, (void));
    LOAD(void, SteamAPI_Shutdown, (void));
    LOAD(void, SteamAPI_RunCallbacks, (void));
    LOAD(void *, SteamAPI_SteamUser_v021, (void));
    LOAD(void *, SteamAPI_SteamUtils_v010, (void));
    LOAD(void *, SteamAPI_SteamUGC_v016, (void));
    LOAD(void *, SteamAPI_SteamRemoteStorage_v016, (void));
    LOAD(bool, SteamAPI_ISteamUser_BLoggedOn, (void *));
    LOAD(uint64_t, SteamAPI_ISteamUser_GetSteamID, (void *));
    LOAD(uint32_t, SteamAPI_ISteamUtils_GetAppID, (void *));
    LOAD(bool, SteamAPI_ISteamUtils_IsAPICallCompleted, (void *, uint64_t, bool *));
    LOAD(bool, SteamAPI_ISteamUtils_GetAPICallResult, (void *, uint64_t, void *, int32_t, int32_t, bool *));
    LOAD(bool, SteamAPI_ISteamRemoteStorage_FileWrite, (void *, const char *, const void *, int32_t));
    LOAD(uint64_t, SteamAPI_ISteamRemoteStorage_CreatePublishedFileUpdateRequest, (void *, uint64_t));
    LOAD(bool, SteamAPI_ISteamRemoteStorage_UpdatePublishedFileFile, (void *, uint64_t, const char *));
    LOAD(bool, SteamAPI_ISteamRemoteStorage_UpdatePublishedFileSetChangeDescription, (void *, uint64_t, const char *));
    LOAD(uint64_t, SteamAPI_ISteamRemoteStorage_CommitPublishedFileUpdate, (void *, uint64_t));
    LOAD(bool, SteamAPI_ISteamUGC_DownloadItem, (void *, uint64_t, bool));
    LOAD(uint32_t, SteamAPI_ISteamUGC_GetItemState, (void *, uint64_t));
    LOAD(bool, SteamAPI_ISteamUGC_GetItemInstallInfo, (void *, uint64_t, uint64_t *, char *, uint32_t, uint32_t *));

    if (!SteamAPI_Init()) fail("Steam is not running or not signed in");
    void *user = SteamAPI_SteamUser_v021(), *utils = SteamAPI_SteamUtils_v010();
    if (!user || !utils || !SteamAPI_ISteamUser_BLoggedOn(user) ||
        SteamAPI_ISteamUtils_GetAppID(utils) != app || SteamAPI_ISteamUser_GetSteamID(user) != owner) {
        SteamAPI_Shutdown();
        fail("the signed-in account or app context does not match the item owner");
    }

    if (!publish) {
        uint32_t minimum = (uint32_t)strtoul(argv[5], &end, 10);
        void *ugc = SteamAPI_SteamUGC_v016();
        if (!minimum || *end || !ugc || !SteamAPI_ISteamUGC_DownloadItem(ugc, ITEM, true)) {
            SteamAPI_Shutdown();
            fail("Steam did not accept the download request");
        }
        for (time_t started = time(NULL); time(NULL) - started < 300; usleep(200000)) {
            SteamAPI_RunCallbacks();
            uint64_t size = 0;
            uint32_t stamp = 0, state = SteamAPI_ISteamUGC_GetItemState(ugc, ITEM);
            char path[4096] = {0};
            if (!(state & STATE_INSTALLED) || (state & STATE_BUSY) ||
                !SteamAPI_ISteamUGC_GetItemInstallInfo(ugc, ITEM, &size, path, sizeof(path), &stamp) || stamp < minimum)
                continue;
            size_t installed_size = 0;
            unsigned char *installed = read_file(path, 10 * 1024 * 1024, &installed_size);
            bool regular = installed != NULL;
            bool same = regular && installed_size == zip_size && !memcmp(installed, zip, zip_size);
            if (strpbrk(path, "\"\\\n")) path[0] = 0;
            receipt(argv[6], "{\"phase\":\"installed\",\"item\":\"%" PRIu64 "\",\"legacy\":%s,\"regularFile\":%s,"
                "\"matchesZip\":%s,\"size\":%zu,\"timestamp\":%u,\"path\":\"%s\"}", ITEM,
                state & STATE_LEGACY ? "true" : "false", regular ? "true" : "false", same ? "true" : "false",
                installed_size, stamp, path);
            SteamAPI_Shutdown();
            return same ? 0 : 2;
        }
        SteamAPI_Shutdown();
        fail("no installed update at or after MIN_TIMESTAMP within five minutes");
    }

    size_t note_size = 0;
    char *note = (char *)read_file(argv[5], 8000, &note_size);
    if (!note) fail("NOTE_FILE must hold 1 to 8000 bytes");
    void *storage = SteamAPI_SteamRemoteStorage_v016();
    const char *name = strrchr(argv[4], '/');
    name = name ? name + 1 : argv[4];
    if (!storage || !SteamAPI_ISteamRemoteStorage_FileWrite(storage, name, zip, (int32_t)zip_size)) {
        SteamAPI_Shutdown();
        fail("Steam Cloud did not accept the ZIP; nothing was submitted");
    }
    uint64_t update = SteamAPI_ISteamRemoteStorage_CreatePublishedFileUpdateRequest(storage, ITEM);
    if (!update || update == UINT64_MAX ||
        !SteamAPI_ISteamRemoteStorage_UpdatePublishedFileFile(storage, update, name) ||
        !SteamAPI_ISteamRemoteStorage_UpdatePublishedFileSetChangeDescription(storage, update, note)) {
        SteamAPI_Shutdown();
        fail("Steam rejected the update request; nothing was submitted");
    }
    receipt(argv[6], "{\"phase\":\"submitting\",\"item\":\"%" PRIu64 "\",\"file\":\"%s\"}", ITEM, name);
    uint64_t call = SteamAPI_ISteamRemoteStorage_CommitPublishedFileUpdate(storage, update);
    for (time_t started = time(NULL); call && time(NULL) - started < 300; usleep(200000)) {
        SteamAPI_RunCallbacks();
        bool failed = false;
        if (!SteamAPI_ISteamUtils_IsAPICallCompleted(utils, call, &failed)) continue;
        UpdateResult result = {0};
        bool read = !failed && SteamAPI_ISteamUtils_GetAPICallResult(utils, call, &result, sizeof(result),
            UPDATE_RESULT_CALLBACK, &failed);
        receipt(argv[6], "{\"phase\":\"%s\",\"item\":\"%" PRIu64 "\",\"file\":\"%s\",\"result\":%d,"
            "\"legalAgreementRequired\":%s,\"call\":\"%" PRIu64 "\"}", read && !failed ? "completed" : "unknown",
            result.item, name, result.result, result.legal ? "true" : "false", call);
        SteamAPI_Shutdown();
        return read && !failed && result.result == 1 && result.item == ITEM && !result.legal ? 0 : 2;
    }
    receipt(argv[6], "{\"phase\":\"unknown\",\"item\":\"%" PRIu64 "\",\"file\":\"%s\",\"call\":\"%" PRIu64 "\"}",
        ITEM, name, call);
    SteamAPI_Shutdown();
    fail("update outcome unknown; check the public readback before another write");
}

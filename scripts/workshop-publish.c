// Workshop release tool for Reveal StarMap; never shipped in the mod.
//
// ONI installs a Workshop mod only when GetItemInstallInfo returns a regular ZIP file
// (KMod.Steam.MakeMod). A modern UGC content update turns the item into a depot directory that
// ONI cannot install, so this tool uses only the legacy RemoteStorage contract of the official
// ONI Mod Uploader (creator app 636750). Gallery images and visibility are edited on the item's
// web page, which leaves its storage legacy; nothing here ever calls an ISteamUGC update.
//
//   workshop-publish SDK create  OWNER new  ZIP LISTING_DIR   RECEIPT
//   workshop-publish SDK publish OWNER ITEM ZIP NOTE_FILE     RECEIPT
//   workshop-publish SDK verify  OWNER ITEM ZIP MIN_TIMESTAMP RECEIPT
//
// create   publishes ZIP as a new private item with LISTING_DIR's title.txt, description.txt and
//          preview.png or preview.jpg (under 1 MB), tagged like the first listing.
// publish  stages ZIP in Steam Cloud and commits it to ITEM with the change note; title,
//          description, tags, preview and visibility stay as they are.
// verify   downloads ITEM as the game (app 457140) and requires a regular file install,
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
static const char *TAGS[] = { "Spaced Out!", "The Frosty Planet Pack", "The Bionic Booster Pack",
    "The Prehistoric Planet Pack", "The Aquatic Planet Pack", "New Features", "tweaks", "ui" };
enum { CREATE_RESULT_CALLBACK = 1309, UPDATE_RESULT_CALLBACK = 1316, VISIBILITY_PRIVATE = 2,
    STATE_LEGACY = 2, STATE_INSTALLED = 4, STATE_BUSY = 8 | 16 | 32 };

// RemoteStoragePublishFileResult_t and RemoteStorageUpdatePublishedFileResult_t share this layout.
#pragma pack(push, 4)
typedef struct { int32_t result; uint64_t item; bool legal; } PublishResult;
#pragma pack(pop)
typedef struct { const char **strings; int32_t count; } StringArray;  // SteamParamStringArray_t
_Static_assert(sizeof(PublishResult) == 16 && offsetof(PublishResult, item) == 4 &&
    offsetof(PublishResult, legal) == 12, "RemoteStorage publish result layout");

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
    if (argc != 8 || (strcmp(argv[2], "create") && strcmp(argv[2], "publish") && strcmp(argv[2], "verify")))
        fail("usage: workshop-publish SDK create|publish|verify OWNER new|ITEM ZIP LISTING_DIR|NOTE_FILE|MIN_TIMESTAMP RECEIPT");
    bool create = !strcmp(argv[2], "create"), verify = !strcmp(argv[2], "verify");
    char *end;
    uint64_t owner = strtoull(argv[3], &end, 10);
    if (!owner || *end) fail("OWNER must be the item owner's SteamID64");
    uint64_t item = create ? 0 : strtoull(argv[4], &end, 10);
    if (create ? strcmp(argv[4], "new") : !item || *end) fail("ITEM must be a published file ID, or new for create");
    if (!access(argv[7], F_OK)) fail("RECEIPT exists; reconcile it before another run");

    size_t zip_size = 0;
    unsigned char *zip = read_file(argv[5], 10 * 1024 * 1024, &zip_size);
    if (!zip || zip_size < 22 || memcmp(zip, "PK\x03\x04", 4)) fail("ZIP must be one ZIP file under 10 MB");

    // Everything a submission needs is read before Steam starts, so a bad input never writes.
    char path[4096], *text = NULL, *title = NULL;
    size_t text_size = 0, title_size = 0, preview_size = 0;
    unsigned char *preview = NULL;
    const char *preview_ext = "png";
    if (create) {
        snprintf(path, sizeof(path), "%s/title.txt", argv[6]);
        title = (char *)read_file(path, 128, &title_size);
        snprintf(path, sizeof(path), "%s/description.txt", argv[6]);
        text = (char *)read_file(path, 8000, &text_size);
        snprintf(path, sizeof(path), "%s/preview.png", argv[6]);
        if (!(preview = read_file(path, 1024 * 1024, &preview_size))) {
            preview_ext = "jpg";
            snprintf(path, sizeof(path), "%s/preview.jpg", argv[6]);
            preview = read_file(path, 1024 * 1024, &preview_size);
        }
        if (!title || !text || !preview)
            fail("LISTING_DIR needs title.txt, description.txt (up to 8000 bytes) and preview.png or preview.jpg under 1 MB");
        title[strcspn(title, "\r\n")] = 0;
        if (!*title) fail("title.txt is empty");
    } else if (!verify && !(text = (char *)read_file(argv[6], 8000, &text_size))) {
        fail("NOTE_FILE must hold 1 to 8000 bytes");
    }

    uint32_t app = verify ? GAME_APP : CREATOR_APP;
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
    LOAD(uint64_t, SteamAPI_ISteamRemoteStorage_PublishWorkshopFile,
        (void *, const char *, const char *, uint32_t, const char *, const char *, int32_t, StringArray *, int32_t));
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

    if (verify) {
        uint32_t minimum = (uint32_t)strtoul(argv[6], &end, 10);
        void *ugc = SteamAPI_SteamUGC_v016();
        if (!minimum || *end || !ugc || !SteamAPI_ISteamUGC_DownloadItem(ugc, item, true)) {
            SteamAPI_Shutdown();
            fail("Steam did not accept the download request");
        }
        for (time_t started = time(NULL); time(NULL) - started < 300; usleep(200000)) {
            SteamAPI_RunCallbacks();
            uint64_t size = 0;
            uint32_t stamp = 0, state = SteamAPI_ISteamUGC_GetItemState(ugc, item);
            char installed_path[4096] = {0};
            if (!(state & STATE_INSTALLED) || (state & STATE_BUSY) ||
                !SteamAPI_ISteamUGC_GetItemInstallInfo(ugc, item, &size, installed_path, sizeof(installed_path), &stamp) ||
                stamp < minimum)
                continue;
            size_t installed_size = 0;
            unsigned char *installed = read_file(installed_path, 10 * 1024 * 1024, &installed_size);
            bool regular = installed != NULL;
            bool same = regular && installed_size == zip_size && !memcmp(installed, zip, zip_size);
            if (strpbrk(installed_path, "\"\\\n")) installed_path[0] = 0;
            receipt(argv[7], "{\"phase\":\"installed\",\"item\":\"%" PRIu64 "\",\"legacy\":%s,\"regularFile\":%s,"
                "\"matchesZip\":%s,\"size\":%zu,\"timestamp\":%u,\"path\":\"%s\"}", item,
                state & STATE_LEGACY ? "true" : "false", regular ? "true" : "false", same ? "true" : "false",
                installed_size, stamp, installed_path);
            SteamAPI_Shutdown();
            return same ? 0 : 2;
        }
        SteamAPI_Shutdown();
        fail("no installed update at or after MIN_TIMESTAMP within five minutes");
    }

    void *storage = SteamAPI_SteamRemoteStorage_v016();
    const char *name = strrchr(argv[5], '/');
    name = name ? name + 1 : argv[5];
    if (!storage || !SteamAPI_ISteamRemoteStorage_FileWrite(storage, name, zip, (int32_t)zip_size)) {
        SteamAPI_Shutdown();
        fail("Steam Cloud did not accept the ZIP; nothing was submitted");
    }
    uint64_t call;
    int32_t callback;
    if (create) {
        char preview_name[300];
        snprintf(preview_name, sizeof(preview_name), "%.*s-preview.%s", (int)strcspn(name, "."), name, preview_ext);
        if (!SteamAPI_ISteamRemoteStorage_FileWrite(storage, preview_name, preview, (int32_t)preview_size)) {
            SteamAPI_Shutdown();
            fail("Steam Cloud did not accept the preview; nothing was submitted");
        }
        StringArray tags = { TAGS, (int32_t)(sizeof(TAGS) / sizeof(*TAGS)) };
        receipt(argv[7], "{\"phase\":\"submitting\",\"item\":\"new\",\"file\":\"%s\"}", name);
        call = SteamAPI_ISteamRemoteStorage_PublishWorkshopFile(storage, name, preview_name, GAME_APP, title, text,
            VISIBILITY_PRIVATE, &tags, 0);
        callback = CREATE_RESULT_CALLBACK;
    } else {
        uint64_t update = SteamAPI_ISteamRemoteStorage_CreatePublishedFileUpdateRequest(storage, item);
        if (!update || update == UINT64_MAX ||
            !SteamAPI_ISteamRemoteStorage_UpdatePublishedFileFile(storage, update, name) ||
            !SteamAPI_ISteamRemoteStorage_UpdatePublishedFileSetChangeDescription(storage, update, text)) {
            SteamAPI_Shutdown();
            fail("Steam rejected the update request; nothing was submitted");
        }
        receipt(argv[7], "{\"phase\":\"submitting\",\"item\":\"%" PRIu64 "\",\"file\":\"%s\"}", item, name);
        call = SteamAPI_ISteamRemoteStorage_CommitPublishedFileUpdate(storage, update);
        callback = UPDATE_RESULT_CALLBACK;
    }
    for (time_t started = time(NULL); call && time(NULL) - started < 300; usleep(200000)) {
        SteamAPI_RunCallbacks();
        bool failed = false;
        if (!SteamAPI_ISteamUtils_IsAPICallCompleted(utils, call, &failed)) continue;
        PublishResult result = {0};
        bool read = !failed && SteamAPI_ISteamUtils_GetAPICallResult(utils, call, &result, sizeof(result),
            callback, &failed);
        receipt(argv[7], "{\"phase\":\"%s\",\"item\":\"%" PRIu64 "\",\"file\":\"%s\",\"result\":%d,"
            "\"legalAgreementRequired\":%s,\"call\":\"%" PRIu64 "\"}", read && !failed ? "completed" : "unknown",
            result.item, name, result.result, result.legal ? "true" : "false", call);
        SteamAPI_Shutdown();
        return read && !failed && result.result == 1 && !result.legal &&
            (create ? result.item != 0 : result.item == item) ? 0 : 2;
    }
    receipt(argv[7], "{\"phase\":\"unknown\",\"item\":\"%s\",\"file\":\"%s\",\"call\":\"%" PRIu64 "\"}",
        argv[4], name, call);
    SteamAPI_Shutdown();
    fail("submission outcome unknown; check the owner's Workshop page before another write");
}

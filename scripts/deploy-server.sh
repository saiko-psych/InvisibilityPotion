#!/usr/bin/env bash
# Update InvisibilityPotion on a Linux dedicated server that already has BepInEx + Jötunn (see scripts/server-setup.sh, docs/server.md).
# Copies only the Release DLL (the asset bundle is embedded) into <path>/BepInEx/plugins/InvisibilityPotion/. Idempotent.
#
# Usage: scripts/deploy-server.sh [--dry-run] [--force] [--dll FILE] <ssh-host> [server-path]
#   ssh-host     anything `ssh` accepts (user@host, or a Host alias from ~/.ssh/config)
#   server-path  Valheim dedicated server directory (contains valheim_server.x86_64), absolute or relative to the remote
#                home (no ~); default $IP_SERVER_PATH
#   --dry-run    show what would be copied, change nothing
#   --force      deploy even while valheim_server.x86_64 runs (restart it afterwards)
#   --dll FILE   DLL to deploy (default InvisibilityPotion/Package/plugins/InvisibilityPotion.dll from `make package`)
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
. "$repo/scripts/server-lib.sh"

DRY_RUN=0; FORCE=0; dll="$repo/$RELEASE_DLL_DEFAULT"; args=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1; shift ;;
    --force)   FORCE=1; shift ;;
    --dll)     dll="$2"; shift 2 ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    -*)        die "unknown option $1" ;;
    *)         args+=("$1"); shift ;;
  esac
done
[ "${#args[@]}" -ge 1 ] || { sed -n '2,13p' "$0"; exit 1; }
host="${args[0]}"
path="${args[1]:-${IP_SERVER_PATH:-}}"
[ -n "$path" ] || die "no server path: pass it as the second argument or set IP_SERVER_PATH"
path="${path%/}"
export DRY_RUN FORCE

need ssh rsync
check_release_dll "$dll"
note "checking $host:$path"
check_server_dir "$host" "$path"
remote_sh "$host" "test -f $(q "$path/BepInEx/core/BepInEx.Preloader.dll")" \
  || die "BepInEx is not installed in $host:$path. Run scripts/server-setup.sh first"
remote_sh "$host" "test -n \"\$(find $(q "$path/BepInEx/plugins") -name Jotunn.dll -print -quit 2>/dev/null)\"" \
  || die "Jotunn.dll not found under $host:$path/BepInEx/plugins. Run scripts/server-setup.sh first"
check_server_stopped "$host"

dest="$path/BepInEx/plugins/InvisibilityPotion"
[ "$DRY_RUN" = "1" ] || remote "$host" mkdir -p "$dest"
note "deploying $(basename "$dll") ($(stat -c %s "$dll") bytes) to $host:$dest/"
if [ "$DRY_RUN" = "1" ] && ! remote_sh "$host" "test -d $(q "$dest")"; then
  note "would create $dest and copy InvisibilityPotion.dll into it"
else
  rsync_to --chmod=F644 "$dll" "$host:$dest/InvisibilityPotion.dll"
fi
[ "$DRY_RUN" = "1" ] && { note "dry run: nothing changed"; exit 0; }
note "done. Restart the server, then check $path/BepInEx/LogOutput.log for 'InvisibilityPotion' and 'Patch health'"

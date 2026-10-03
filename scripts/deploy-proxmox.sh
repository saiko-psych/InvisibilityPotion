#!/usr/bin/env bash
# Deploy the Release DLL to the author's Valheim server: LXC container CT 132 behind the Proxmox host `proxymoxy`.
# The server loads plugins from an overlay directory inside the container; the DLL goes there under the name InvisibleMod.dll
# (BepInEx loads a plugin DLL under any file name; the plugin still logs as InvisibilityPotion). See docs/server.md,
# "Proxmox overlay deployment (the author's setup)". server-setup.sh / deploy-server.sh are not for this server.
#
# Builds nothing: run `make package` first (Release). Refuses a Debug DLL.
#
# Usage: scripts/deploy-proxmox.sh [--dry-run] [--logs]
#   (no option)  stop the valheim unit, scp the DLL to the Proxmox host, `pct push` it into the overlay, start valheim-gate
#                (CT 132 runs on demand: the gate wakes the server on the first connection and run-server.sh mirrors the
#                overlay via apply-mods.sh; NEVER `systemctl start valheim` directly, it bypasses the gate)
#   --logs       show the last 5 minutes of the valheim unit's journal (server must be running: connect first)
#   --dry-run    print the commands instead of running them
# Environment (defaults in brackets):
#   HOST    Proxmox host for ssh/scp [proxymoxy]; logged in as root
#   CT      container id [132]
#   TARGET  plugin directory inside the container [/opt/valheim/mods/overlay/BepInEx/plugins]
#   NAME    file name of the DLL in TARGET [InvisibleMod.dll]
#   DLL     local DLL [InvisibilityPotion/bin/Release/net48/InvisibilityPotion.dll, written by `make package`]
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
. "$repo/scripts/server-lib.sh"

HOST="${HOST:-proxymoxy}"
CT="${CT:-132}"
TARGET="${TARGET:-/opt/valheim/mods/overlay/BepInEx/plugins}"
NAME="${NAME:-InvisibleMod.dll}"
DLL="${DLL:-$repo/InvisibilityPotion/bin/Release/net48/InvisibilityPotion.dll}"
TARGET="${TARGET%/}"

DRY_RUN=0; MODE=deploy
while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1; shift ;;
    --logs)    MODE=logs; shift ;;
    -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
    *)         die "unknown argument $1 (see --help)" ;;
  esac
done
[[ "$CT" =~ ^[0-9]+$ ]] || die "CT must be a numeric container id, got '$CT'"
case "$NAME" in */*|"") die "NAME must be a plain file name, got '$NAME'" ;; esac

# Local command: printed (dry run) or run.
run() {
  if [ "$DRY_RUN" = "1" ]; then printf '%q ' "$@"; echo; else "$@"; fi
}
# Remote shell command on the Proxmox host: printed in the form the author types it (dry run) or run.
run_ssh() {
  if [ "$DRY_RUN" = "1" ]; then echo "ssh root@$HOST \"$1\""; else ssh "root@$HOST" "$1"; fi
}

if [ "$MODE" = "logs" ]; then
  need ssh
  run_ssh "pct exec $CT -- journalctl -u valheim --no-pager --since '-5 min' | grep -iE 'Loading \\[|InvisibleMod|InvisibilityPotion|exception|Patch'"
  exit 0
fi

need ssh scp
check_release_dll "$DLL"
tmp="/tmp/$NAME"
note "deploying $DLL ($(stat -c %s "$DLL") bytes) to $HOST CT $CT:$TARGET/$NAME"
run_ssh "pct exec $CT -- systemctl stop valheim"
run scp "$DLL" "root@$HOST:$tmp"
run_ssh "pct push $CT $(q "$tmp") $(q "$TARGET/$NAME")"
run_ssh "pct exec $CT -- systemctl start valheim-gate"
[ "$DRY_RUN" = "1" ] && { note "dry run: nothing changed"; exit 0; }
note "done. The gate is armed: connect in game to wake the server (apply-mods.sh mirrors the overlay); then: scripts/deploy-proxmox.sh --logs"
note "if an old InvisibilityPotion.dll exists, remove it from BOTH $TARGET and /opt/valheim/server/BepInEx/plugins (the mirror never deletes)"

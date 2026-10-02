#!/usr/bin/env bash
# One-time setup of a vanilla Linux Valheim dedicated server for InvisibilityPotion: installs BepInExPack_Valheim, Jötunn and
# the Release DLL over SSH. Idempotent: rerunning updates the files and never overwrites an existing BepInEx/config/BepInEx.cfg
# or start_server_bepinex.sh. Nothing is deleted on the server. See docs/server.md for the order of steps and verification.
#
# Usage: scripts/server-setup.sh [--dry-run] [--force] [--dll FILE] [--bepinex VERSION] [--jotunn VERSION] <ssh-host> <server-path>
#   ssh-host     anything `ssh` accepts (user@host, or a Host alias from ~/.ssh/config); key-based login (BatchMode)
#   server-path  Valheim dedicated server directory (contains valheim_server.x86_64), absolute or relative to the remote home (no ~)
#   --dry-run    download and stage locally, then show what rsync would copy; change nothing on the server
#   --force      continue while valheim_server.x86_64 runs (restart it afterwards)
#   --dll FILE   our DLL (default InvisibilityPotion/Package/plugins/InvisibilityPotion.dll from `make package`)
#   --bepinex V  denikson-BepInExPack_Valheim version (default 5.4.2351, the manifest dependency)
#   --jotunn V   ValheimModding-Jotunn version (default 2.30.2, the manifest dependency)
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
. "$repo/scripts/server-lib.sh"

DRY_RUN=0; FORCE=0; dll="$repo/$RELEASE_DLL_DEFAULT"; bep_ver="5.4.2351"; jot_ver="2.30.2"; args=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1; shift ;;
    --force)   FORCE=1; shift ;;
    --dll)     dll="$2"; shift 2 ;;
    --bepinex) bep_ver="$2"; shift 2 ;;
    --jotunn)  jot_ver="$2"; shift 2 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    -*)        die "unknown option $1" ;;
    *)         args+=("$1"); shift ;;
  esac
done
[ "${#args[@]}" -eq 2 ] || { sed -n '2,16p' "$0"; exit 1; }
host="${args[0]}"; path="${args[1]%/}"
export DRY_RUN FORCE

need ssh rsync curl python3
check_release_dll "$dll"

# ---- (a) server directory ----
note "checking $host:$path"
check_server_dir "$host" "$path"
check_server_stopped "$host"

# ---- (b, c) download and stage locally (build/ is gitignored) ----
cache="$repo/build/server"
stage="$cache/stage"
mkdir -p "$cache"
download() {   # thunderstore namespace, name, version -> zip path
  local zip="$cache/$1-$2-$3.zip"
  if [ ! -s "$zip" ]; then
    note "downloading $1-$2-$3 from Thunderstore" >&2
    curl -fsSL -o "$zip.part" "https://thunderstore.io/package/download/$1/$2/$3/" || die "download of $1-$2-$3 failed"
    mv "$zip.part" "$zip"
  fi
  echo "$zip"
}
bep_zip="$(download denikson BepInExPack_Valheim "$bep_ver")"
jot_zip="$(download ValheimModding Jotunn "$jot_ver")"

rm -rf "$stage"; mkdir -p "$stage"
# python3 instead of unzip: the Jötunn package stores its paths with backslashes (plugins\Jotunn.dll).
python3 - "$bep_zip" "$jot_zip" "$stage" <<'PY'
import os, sys, zipfile
bep, jot, stage = sys.argv[1:4]
PREFIX = "BepInExPack_Valheim/"
SKIP = {"winhttp.dll", "start_game_bepinex.sh"}          # Windows loader and client launcher: not for a Linux server
need = {"start_server_bepinex.sh", "doorstop_config.ini", "BepInEx/core/BepInEx.Preloader.dll", "doorstop_libs/libdoorstop_x64.so"}
with zipfile.ZipFile(bep) as z:
    for info in z.infolist():
        name = info.filename.replace("\\", "/")
        if not name.startswith(PREFIX) or name.endswith("/"): continue
        rel = name[len(PREFIX):]
        if rel in SKIP: continue
        out = os.path.join(stage, rel)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        with z.open(info) as src, open(out, "wb") as dst: dst.write(src.read())
        need.discard(rel)
if need: sys.exit(f"BepInExPack layout changed, missing: {sorted(need)}")
found = False
with zipfile.ZipFile(jot) as z:
    for info in z.infolist():
        name = info.filename.replace("\\", "/")
        if name == "plugins/Jotunn.dll":
            out = os.path.join(stage, "BepInEx", "plugins", "Jotunn", "Jotunn.dll")
            os.makedirs(os.path.dirname(out), exist_ok=True)
            with z.open(info) as src, open(out, "wb") as dst: dst.write(src.read())
            found = True
if not found: sys.exit("Jotunn package layout changed: plugins/Jotunn.dll not found")
PY
mkdir -p "$stage/BepInEx/plugins/InvisibilityPotion"
cp "$dll" "$stage/BepInEx/plugins/InvisibilityPotion/InvisibilityPotion.dll"
chmod -R u=rwX,go=rX "$stage"
chmod 755 "$stage/start_server_bepinex.sh"
note "staged in $stage: BepInExPack_Valheim $bep_ver, Jotunn $jot_ver, $(basename "$dll")"

# ---- (b, c, d) copy to the server ----
# Pass 1: everything except files a server owner customises. Pass 2: those only when missing.
note "copying BepInEx, doorstop, Jotunn and InvisibilityPotion to $host:$path/"
rsync_to -p --exclude=/BepInEx/config/ --exclude=/start_server_bepinex.sh "$stage/" "$host:$path/"
rsync_to -p --ignore-existing --include=/start_server_bepinex.sh --include=/BepInEx/ --include=/BepInEx/config/ \
  --include=/BepInEx/config/** --exclude='*' "$stage/" "$host:$path/"

# ---- (e) next steps ----
cat <<EOF

$( [ "$DRY_RUN" = "1" ] && echo "DRY RUN: nothing was changed on $host." || echo "Installed on $host:$path." )

Next steps (docs/server.md has the details):
1. Start the server through BepInEx instead of valheim_server.x86_64 directly. start_server_bepinex.sh sets the doorstop
   variables and then runs:  exec ./valheim_server.x86_64 -name "My server" -port 2456 -world "Dedicated" -password "secret"
   Copy it once and edit the arguments in the copy (the pack overwrites the original on updates; this script never does):
     ssh $host 'cd $(q "$path") && cp -n start_server_bepinex.sh start_server_ip.sh && chmod +x start_server_ip.sh'
     then edit -name/-world/-password (and -public, -crossplay as before) in start_server_ip.sh
2. systemd unit example (adjust User; paths must be absolute; installing it needs sudo):
     [Unit]
     Description=Valheim dedicated server (BepInEx)
     After=network-online.target
     [Service]
     User=valheim
     WorkingDirectory=$path
     ExecStart=$path/start_server_ip.sh
     KillSignal=SIGINT
     Restart=on-failure
     [Install]
     WantedBy=multi-user.target
3. Start the server, then verify:  ssh $host "grep -E 'Chainloader|Jotunn|InvisibilityPotion|Patch health' $(q "$path/BepInEx/LogOutput.log")"
   Expect "InvisibilityPotion 0.3.x loaded" and "Patch health: N targets patched, 0 missing".
4. Later updates: make package && scripts/deploy-server.sh $host $(q "$path")
EOF

# Shared helpers for server-setup.sh, deploy-server.sh and deploy-proxmox.sh (sourced by bash, not run).
# No secrets here: the SSH host is whatever `ssh <host>` resolves (use ~/.ssh/config for user, port and key).

RELEASE_DLL_DEFAULT="InvisibilityPotion/Package/plugins/InvisibilityPotion.dll"   # written by `make package`
SERVER_BINARY="valheim_server.x86_64"

die() { echo "error: $*" >&2; exit 1; }
note() { echo "==> $*"; }

need() { for c in "$@"; do command -v "$c" > /dev/null || die "$c is not installed"; done; }

# Remote command with every argument quoted for the remote shell (paths with spaces survive).
remote() {
  local host="$1" cmd="" a; shift
  for a in "$@"; do cmd="$cmd $(printf '%q' "$a")"; done
  ssh -o BatchMode=yes "$host" "$cmd"
}

# Remote shell snippet as one string (for tests with && / ||); the caller quotes paths with q.
remote_sh() { ssh -o BatchMode=yes "$1" "$2"; }
q() { printf '%q' "$1"; }

# The DLL must be a Release build: Debug builds contain the dev console commands (ip_give etc., UTF-16 literals).
check_release_dll() {
  local dll="$1"
  [ -f "$dll" ] || die "$dll not found. Build it first: make package (Release; refuses while the game runs, the build itself never touches the game)"
  # grep -c reads everything: grep -q would SIGPIPE strings, and pipefail turns that into "no match".
  if command -v strings > /dev/null && [ "$(strings -a -e l "$dll" | grep -c '^ip_give$' || true)" != "0" ]; then
    die "$dll is a Debug build (contains the ip_give dev command). Use the Release DLL from make package"
  fi
}

check_server_dir() {
  local host="$1" path="$2"
  remote_sh "$host" "test -f $(q "$path/$SERVER_BINARY")" \
    || die "$host:$path does not contain $SERVER_BINARY (wrong path, or not a Valheim dedicated server)"
}

# Refuses to touch a running server unless FORCE=1: the plugin DLL is loaded at startup, a swap needs a restart anyway.
check_server_stopped() {
  local host="$1"
  if remote_sh "$host" "pgrep -f '[v]alheim_server\.x86_64' > /dev/null"; then
    if [ "${FORCE:-0}" = "1" ]; then note "valheim_server.x86_64 is running on $host; continuing (--force). Restart it afterwards"
    elif [ "${DRY_RUN:-0}" = "1" ]; then note "warning: valheim_server.x86_64 is running on $host; a real run refuses without --force"
    else die "valheim_server.x86_64 is running on $host. Stop the server first (or pass --force and restart it afterwards)"
    fi
  fi
}

# rsync with spaces-safe remote paths (-s); -n in dry-run mode.
rsync_to() {
  if [ "${DRY_RUN:-0}" = "1" ]; then set -- -n "$@"; fi
  rsync -s -rlt --itemize-changes "$@"
}

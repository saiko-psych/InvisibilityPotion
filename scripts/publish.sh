#!/bin/sh
# Post-build publish step. Debug: copy dll+pdb into BepInEx/plugins/<name>/. Release: build the Thunderstore zip.
# Adapted from JotunnModStub (MIT-0): https://github.com/Valheim-Modding/JotunnModStub

target="Debug"
targetPath=""
targetAssembly="InvisibilityPotion.dll"
valheimPath=""
bepinexPath=""
deployPath=""
projectPath="./InvisibilityPotion"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --target)          target="$2"; shift 2 ;;
    --target-path)     targetPath="$2"; shift 2 ;;
    --target-assembly) targetAssembly="$2"; shift 2 ;;
    --valheim-path)    valheimPath="$2"; shift 2 ;;
    --bepinex-path)    bepinexPath="$2"; shift 2 ;;
    --deploy-path)     deployPath="$2"; shift 2 ;;
    --project-path)    projectPath="$2"; shift 2 ;;
    *) echo "Warning: Unknown argument $1" >&2; shift ;;
  esac
done

# precedence: MOD_DEPLOYPATH > BEPINEX_PATH > VALHEIM_INSTALL > default
if [ -z "$deployPath" ]; then
  if [ -n "$bepinexPath" ]; then deployPath="$bepinexPath/plugins"
  elif [ -n "$valheimPath" ]; then deployPath="$valheimPath/BepInEx/plugins"
  else deployPath="$HOME/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins"
  fi
fi

name=$(echo "$targetAssembly" | sed 's/\.dll$//')

if [ "$target" = "Debug" ]; then
  plug="$deployPath/$name"
  echo "Deploying $targetAssembly to $plug"
  mkdir -p "$plug"
  cp "$targetPath/$targetAssembly" "$plug/"
  [ -e "$targetPath/$name.pdb" ] && cp "$targetPath/$name.pdb" "$plug/"
fi

if [ "$target" = "Release" ]; then
  packagePath="$projectPath/Package"
  mkdir -p "$packagePath/plugins"
  cp "$targetPath/$targetAssembly" "$packagePath/plugins/"
  if command -v zip > /dev/null; then
    [ -e "$projectPath/$name.zip" ] && rm "$projectPath/$name.zip"
    (cd "$packagePath" && zip -r "../$name.zip" . -x '*.zip' > /dev/null)
    echo "Package ready: $(realpath "$projectPath/$name.zip")"
  else
    echo "zip not installed, skipping package"
  fi
fi

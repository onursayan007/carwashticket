#!/usr/bin/env bash
# Azure CLI'yi konteyner içinde açar — Homebrew veya sudo gerekmez.
#
#   ./azure.sh          -> Azure kabuğu açar (az login, ./deploy-azure.sh burada çalışır)
#   ./azure.sh <komut>  -> tek komut çalıştırıp çıkar
#
# Giriş bilgisi ~/.azure altında saklanır, her seferinde tekrar giriş yapmazsın.

set -euo pipefail

export PATH="$HOME/.docker/bin:$PATH"

command -v docker >/dev/null || { echo "Docker bulunamadı. Docker Desktop kurulu mu?"; exit 1; }

if ! docker info >/dev/null 2>&1; then
  echo "Docker Desktop açılıyor, bekle..."
  open -a Docker 2>/dev/null || true

  for _ in $(seq 1 40); do
    docker info >/dev/null 2>&1 && break
    sleep 3
  done

  docker info >/dev/null 2>&1 || { echo "Docker açılamadı. Docker Desktop'ı elle başlat."; exit 1; }
fi

# Oturumun kalıcı olması için ~/.azure dışarıda tutuluyor.
mkdir -p "$HOME/.azure"

docker run -it --rm \
  -v "$HOME/.azure:/root/.azure" \
  -v "$PWD:/work" \
  -w /work \
  mcr.microsoft.com/azure-cli:latest \
  "${@:-bash}"

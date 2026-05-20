#!/bin/bash
# Запуск TCP-сервера (фото + чат) на Linux.
# Скопируйте на сервер вместе с Server.cs и PhotoServer.csproj, затем:
#   chmod +x start-server.sh
#   ./start-server.sh

set -e
APP_DIR="${PHOTOSERVER_DIR:-/home/ivanesscob/PhotoServer/PhotoServerApp}"
cd "$APP_DIR"

if [ -f "$APP_DIR/PhotoServer.dll" ]; then
  exec dotnet "$APP_DIR/PhotoServer.dll"
fi

if [ -f "$APP_DIR/Server.cs" ]; then
  dotnet run --project "$APP_DIR/PhotoServer.csproj" -c Release
  exit $?
fi

echo "Не найден PhotoServer.dll или Server.cs в $APP_DIR"
exit 1

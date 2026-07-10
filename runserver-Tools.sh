#!/usr/bin/env bash
dotnet run --project Content.Server --configuration Tools -- --data-dir Resources
read -p "Press enter to continue"

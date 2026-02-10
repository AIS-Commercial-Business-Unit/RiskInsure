#!/bin/bash
#set -e 

# From https://github.com/Azure/cosmosdb-emulator-recipes/blob/main/dotnet/linux/entrypoint.sh

cosmosHost=$1
cosmosPort=$2
assemblyName=$3

# This will allow the container to pull the self-signed CosmosDB 
# SSL certificate and add it to the trusted certs, which is required
# for secure communication with CosmosDB Emulator

# This runs in the docker container once it is running, but 
# before the dotnet application starts.

echo "Waiting for CosmosDB to be available at $cosmosHost:$cosmosPort..."
# We need the --insecure flag because we can't connect securely
# until after we have the SSL certificate.  And we can't get the 
# SSL certificate at container build time because it's regenerated
# every time the CosmosDB Emulator container starts.
until [ "$(curl --insecure --silent --connect-timeout 5 -o /dev/null --write-out "%{http_code}" https://$cosmosHost:${cosmosPort}/_explorer/emulator.pem)" == "200" ]; do
    sleep 5;
    echo "Waiting for CosmosDB at $cosmosHost:$cosmosPort..."
done;
echo "CosmosDB is available."

# Download the CosmosDB Cert and add it to the Trusted Certs
echo "Downloading CosmosDB Cert..."
# See note above about the --insecure flag
curl --insecure https://$cosmosHost:${cosmosPort}/_explorer/emulator.pem > cosmosemulatorcert.crt

echo "Adding CosmosDB Cert to Trusted Certs..."
cp cosmosemulatorcert.crt /usr/local/share/ca-certificates/
update-ca-certificates

#echo "-------------------"
# echo "Openssl diagnostics"
# openssl s_client -connect $cosmosHost:$cosmosPort -showcerts
#echo "-------------------"

#echo "-------------------"
# echo "Dotnet Info"
# dotnet --info
#echo "-------------------"

echo "Starting ${assemblyName}..."
dotnet ${assemblyName}
#!/bin/bash
#set -e 

# From https://github.com/Azure/cosmosdb-emulator-recipes/blob/main/dotnet/linux/entrypoint.sh

Green='\033[0;32m'        # Green
Red='\033[0;31m'          # Red
Yellow='\033[0;33m'       # Yellow
Color_Off='\033[0m'       # Text Reset

import_ssl_cert_and_verify() {
    # This will allow the container to pull a self-signed 
    # SSL certificate and add it to the trusted certs

    local server_name="$1"
    local server_port="$2"
    local test_path="$3"

    echo -e "${Green}Importing SSL Cert from https://${server_name}:${server_port} and verifying secure connection...${Color_Off}"

    testUrl="https://${server_name}:${server_port}${test_path}"

    until [ "$(curl --insecure --silent --connect-timeout 5 -o /dev/null --write-out "%{http_code}" "$testUrl")" != "000" ]; do
        sleep 5;
        echo -e "${Yellow}Waiting for a response from $testUrl...${Color_Off}"
    done;
    echo -e "${Green}Server with SSL certificate is available${Color_Off}"

    echo -e "${Green}Downloading SSL Cert...${Color_Off}"
    echo | openssl s_client -showcerts -servername "$server_name" -connect "$server_name:$server_port" 2>/dev/null | openssl x509 -inform pem -outform pem > "${server_name}.crt"

    echo -e "${Green}Adding SSL Cert for $server_name to Trusted Certs...${Color_Off}"
    cp "${server_name}.crt" /usr/local/share/ca-certificates/
    update-ca-certificates

    echo -e "${Green}Verifying we can connect securely to $test_connect_url...${Color_Off}"
    local test_connect_url="https://${server_name}:${server_port}${test_path}"
    local test_connect_content
    test_connect_content=$(mktemp)
    local test_connect_http_code
    test_connect_http_code=$(curl --silent --connect-timeout 10 --output "$test_connect_content" --write-out "%{http_code}" "$test_connect_url")

    if [[ $test_connect_http_code != 000 ]]; then
        echo -e "${Green}Successfully connected to $server_name:$server_port with imported SSL certificate.${Color_Off}"
    else
        echo -e "${Red}Unable to connect securely to $server_name:$server_port. HTTP status code: $test_connect_http_code${Color_Off}"
        echo "Response content:"
        cat "$test_connect_content"
        rm -f "$test_connect_content"
        exit $test_connect_http_code
    fi
    rm -f "$test_connect_content"
}

# Check if the environment variable is set
if [ -z "$IMPORT_SSL_CERTS_FOR" ]; then
    echo -e "${Red}IMPORT_SSL_CERTS_FOR is not set. Exiting.${Color_Off}"
    exit 1
fi
# De-serialize the comma-separated string into a bash array
IFS=',' read -r -a importSslCertsFor <<< "$IMPORT_SSL_CERTS_FOR"

for containerName in "${importSslCertsFor[@]}"; do
    case $containerName in
        "cosmosdb")
            echo -e "${Green}Importing Cosmos DB Emulator SSL Certificate${Color_Off}"
            import_ssl_cert_and_verify "cosmos.domain" "8081" "/"
#            import_ssl_cert_and_verify "cosmos.domain" "8081" "/_explorer/emulator.pem1"
            ;;
        "file-retrieval-https")
            echo -e "${Green}Importing File Retrieval SSL Certificate${Color_Off}"
            import_ssl_cert_and_verify "file-retrieval-https" "443" "/"
            ;;
        "keyvault-emulator")
            echo -e "${Green}Importing Key Vault Emulator SSL Certificate${Color_Off}"
            import_ssl_cert_and_verify "keyvault-emulator" "4997" "/"
            ;;
        *)
            echo -e "${Yellow}Unknown container name '$containerName' in IMPORT_SSL_CERTS_FOR. Skipping SSL cert import for this entry.${Color_Off}"
            ;;
    esac
done

assemblyName=$ASSEMBLY_NAME_TO_RUN

#echo "-------------------"
# echo "Openssl diagnostics"
# openssl s_client -connect $serverName:$serverPort -showcerts
#echo "-------------------"

#echo "-------------------"
# echo "Dotnet Info"
# dotnet --info
#echo "-------------------"

echo -e "${Green}Starting ${assemblyName}...${Color_Off}"
dotnet ${assemblyName}
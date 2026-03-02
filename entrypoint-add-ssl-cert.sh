#!/bin/bash
#set -e 

# From https://github.com/Azure/cosmosdb-emulator-recipes/blob/main/dotnet/linux/entrypoint.sh

import_ssl_cert_and_verify() {
    # This will allow the container to pull a self-signed 
    # SSL certificate and add it to the trusted certs

    local server_name="$1"
    local server_port="$2"
    local test_path="$3"

    echo "Importing SSL Cert from ${server_name}:${server_port} and verifying secure connection..."

    until [ "$(curl --insecure --silent --connect-timeout 5 -o /dev/null --write-out "%{http_code}" "https://${server_name}:${server_port}${test_path}")" == "200" ]; do
        sleep 5;
        echo "Waiting for a HTTP 200 response from ${server_name}:${server_port}${test_path}..."
    done;
    echo "Server with SSL certificate is available"

    echo "Downloading SSL Cert..."
    echo | openssl s_client -showcerts -servername "$server_name" -connect "$server_name:$server_port" 2>/dev/null | openssl x509 -inform pem -outform pem > "${server_name}.crt"

    echo "Adding SSL Cert for $server_name to Trusted Certs..."
    cp "${server_name}.crt" /usr/local/share/ca-certificates/
    update-ca-certificates

    echo "Verifying we can connect securely to Server..."
    local test_connect_url="https://${server_name}:${server_port}${test_path}"
    local test_connect_content
    test_connect_content=$(mktemp)
    local test_connect_http_code
    test_connect_http_code=$(curl --silent --connect-timeout 10 --output "$test_connect_content" --write-out "%{http_code}" "$test_connect_url")

    if [[ $test_connect_http_code == 200 ]]; then
        echo "Successfully connected to $server_name:$server_port with imported SSL certificate."
    else
        echo "Unable to connect securely to $server_name:$server_port. HTTP status code: $test_connect_http_code"
        echo "Response content:"
        cat "$test_connect_content"
        rm -f "$test_connect_content"
        exit $test_connect_http_code
    fi
    rm -f "$test_connect_content"
}

serverName1=$SSL_SERVER_DOMAIN_1
serverPort1=$SSL_SERVER_PORT_1
testPath1=$SSL_TEST_PATH_1 # /_explorer/emulator.pem

serverName2=$SSL_SERVER_DOMAIN_2
serverPort2=$SSL_SERVER_PORT_2
testPath2=$SSL_TEST_PATH_2

assemblyName=$ASSEMBLY_NAME_TO_RUN

import_ssl_cert_and_verify "$serverName1" "$serverPort1" "$testPath1"

if [[ -n "$serverName2" && -n "$serverPort2" && -n "$testPath2" ]]; then
    import_ssl_cert_and_verify "$serverName2" "$serverPort2" "$testPath2"
fi

#echo "-------------------"
# echo "Openssl diagnostics"
# openssl s_client -connect $serverName:$serverPort -showcerts
#echo "-------------------"

#echo "-------------------"
# echo "Dotnet Info"
# dotnet --info
#echo "-------------------"

echo "Starting ${assemblyName}..."
dotnet ${assemblyName}
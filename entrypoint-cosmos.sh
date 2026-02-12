#!/bin/sh

ip_addresses=$(hostname -I)

echo "Setting the IP address override for Cosmos DB Emulator to: $ip_addresses"

# Tell cosmos the actual IP addresses of the container
export AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=$ip_addresses

# Starting the Cosmos DB Emulator
cd /usr/local/bin/cosmos/bin
/usr/local/bin/cosmos/bin/start.sh
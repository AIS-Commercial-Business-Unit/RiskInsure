#!/bin/sh

ip_addresses=$(hostname -I)

Green='\033[0;32m'        # Green
Red='\033[0;31m'          # Red
Yellow='\033[0;33m'       # Yellow
Color_Off='\033[0m'       # Text Reset

echo -e "${Green}Setting the IP address override for Cosmos DB Emulator to: $ip_addresses${Color_Off}"
export AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=$ip_addresses

echo -e "${Green}Starting Cosmos DB Emulator...${Color_Off}"
cd /usr/local/bin/cosmos/bin
/usr/local/bin/cosmos/bin/start.sh
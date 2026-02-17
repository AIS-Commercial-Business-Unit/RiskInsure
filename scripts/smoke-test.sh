#!/bin/bash
#
# Quick smoke test of locally running RiskInsure services
# Verifies Docker containers are running and API endpoints are accessible.
# Non-destructive read-only checks. Completes in 10-15 seconds.
#
# Usage:
#   ./scripts/smoke-test.sh
#   ./scripts/smoke-test.sh --verbose
#

set +e  # Continue on errors
VERBOSE=false

if [[ "$1" == "--verbose" || "$1" == "-v" ]]; then
    VERBOSE=true
fi

START_TIME=$(date +%s)
PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo ""
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN} RiskInsure Local Smoke Test${NC}"
echo -e "${CYAN}========================================${NC}"
echo -e "${GRAY}Started: $(date '+%Y-%m-%d %H:%M:%S')${NC}"
echo ""

# Change to repository root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$REPO_ROOT"

# Step 1: Check Docker
echo -e "${CYAN}[DOCKER STATUS]${NC}"
if docker version &>/dev/null; then
    echo -e "  ${GREEN}Docker daemon: Running${NC}"
    ((PASS_COUNT++))
else
    echo -e "  ${RED}Docker daemon: Not responding${NC}"
    ((FAIL_COUNT++))
    echo ""
    echo -e "${CYAN}[OVERALL RESULT]${NC}"
    echo -e "${RED}FAIL - Docker is not running${NC}"
    exit 1
fi
echo ""

# Step 2: Check Containers
echo -e "${CYAN}[CONTAINER STATUS]${NC}"

EXPECTED_CONTAINERS=(
    "riskinsure-billing-api-1"
    "riskinsure-billing-endpoint-1"
    "riskinsure-customer-api-1"
    "riskinsure-customer-endpoint-1"
    "riskinsure-fundstransfermgt-api-1"
    "riskinsure-fundstransfermgt-endpoint-1"
    "riskinsure-policy-api-1"
    "riskinsure-policy-endpoint-1"
    "riskinsure-ratingandunderwriting-api-1"
    "riskinsure-ratingandunderwriting-endpoint-1"
)

RUNNING_COUNT=0
for CONTAINER in "${EXPECTED_CONTAINERS[@]}"; do
    STATUS=$(docker ps -a --filter "name=^${CONTAINER}\$" --format "{{.Status}}" 2>/dev/null)

    if [[ -z "$STATUS" ]]; then
        echo -e "  ${RED}${CONTAINER} - NOT FOUND${NC}"
        ((FAIL_COUNT++))
    elif [[ "$STATUS" == Up* ]]; then
        echo -e "  ${GREEN}${CONTAINER}${NC} ${GRAY}(${STATUS})${NC}"
        ((RUNNING_COUNT++))
    elif [[ "$STATUS" == Exited* ]]; then
        echo -e "  ${RED}${CONTAINER}${NC} ${GRAY}(${STATUS})${NC}"
        ((FAIL_COUNT++))

        if [[ "$VERBOSE" == "true" ]]; then
            echo -e "    ${YELLOW}Last 10 log lines:${NC}"
            docker logs "$CONTAINER" --tail 10 2>&1 | sed 's/^/      /' | while IFS= read -r line; do
                echo -e "      ${GRAY}${line}${NC}"
            done
        fi
    else
        echo -e "  ${YELLOW}${CONTAINER}${NC} ${GRAY}(${STATUS})${NC}"
        ((WARN_COUNT++))
    fi
done

echo ""
if [[ $RUNNING_COUNT -eq 10 ]]; then
    echo -e "  ${GREEN}Summary: ${RUNNING_COUNT}/10 containers running${NC}"
elif [[ $RUNNING_COUNT -ge 8 ]]; then
    echo -e "  ${YELLOW}Summary: ${RUNNING_COUNT}/10 containers running${NC}"
else
    echo -e "  ${RED}Summary: ${RUNNING_COUNT}/10 containers running${NC}"
fi
echo ""

# Step 3: Test API Endpoints
echo -e "${CYAN}[API CONNECTIVITY]${NC}"

declare -A API_ENDPOINTS=(
    ["Billing"]="http://127.0.0.1:7071"
    ["Customer"]="http://127.0.0.1:7073"
    ["FundsTransferMgt"]="http://127.0.0.1:7075"
    ["Policy"]="http://127.0.0.1:7077"
    ["RatingAndUnderwriting"]="http://127.0.0.1:7079"
)

API_PASS_COUNT=0
# Sort API names for consistent output
SORTED_APIS=($(for k in "${!API_ENDPOINTS[@]}"; do echo "$k"; done | sort))

for API in "${SORTED_APIS[@]}"; do

    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 3 "$URL" 2>/dev/null)

    if [[ "$HTTP_CODE" == "200" ]]; then
        echo -e "  ${GREEN}${API} API: ${URL}${NC} ${GRAY}(${HTTP_CODE})${NC}"
        ((API_PASS_COUNT++))
        ((PASS_COUNT++))
    elif [[ "$HTTP_CODE" == "404" ]]; then
        echo -e "  ${GREEN}${API} API: ${URL}${NC} ${GRAY}(404 - No root endpoint)${NC}"
        ((API_PASS_COUNT++))
        ((PASS_COUNT++))
    elif [[ -z "$HTTP_CODE" || "$HTTP_CODE" == "000" ]]; then
        echo -e "  ${RED}${API} API: ${URL}${NC} ${GRAY}(Connection refused)${NC}"
        ((FAIL_COUNT++))
    else
        echo -e "  ${YELLOW}${API} API: ${URL}${NC} ${GRAY}(HTTP ${HTTP_CODE})${NC}"
        ((WARN_COUNT++))
    fi
done

echo ""
if [[ $API_PASS_COUNT -eq 5 ]]; then
    echo -e "  ${GREEN}Summary: ${API_PASS_COUNT}/5 APIs responding${NC}"
elif [[ $API_PASS_COUNT -ge 4 ]]; then
    echo -e "  ${YELLOW}Summary: ${API_PASS_COUNT}/5 APIs responding${NC}"
else
    echo -e "  ${RED}Summary: ${API_PASS_COUNT}/5 APIs responding${NC}"
fi
echo ""

# Step 4: Check Configuration
echo -e "${CYAN}[CONFIGURATION]${NC}"

if [[ -f ".env" ]]; then
    echo -e "  ${GREEN}.env file: Found${NC}"
    ((PASS_COUNT++))

    if grep -q "COSMOSDB_CONNECTION_STRING=AccountEndpoint=https://" .env; then
        echo -e "  ${GREEN}Cosmos DB connection: Valid format${NC}"
        ((PASS_COUNT++))
    else
        echo -e "  ${RED}Cosmos DB connection: Invalid or missing${NC}"
        ((FAIL_COUNT++))
    fi

    if grep -q "RABBITMQ_CONNECTION_STRING=host=" .env; then
        echo -e "  ${GREEN}RabbitMQ connection: Valid format${NC}"
        ((PASS_COUNT++))
    else
        echo -e "  ${RED}RabbitMQ connection: Invalid or missing${NC}"
        ((FAIL_COUNT++))
    fi
else
    echo -e "  ${RED}.env file: NOT FOUND${NC}"
    ((FAIL_COUNT+=3))
    echo -e "    ${YELLOW}Run: cp .env.example .env and configure connection strings${NC}"
fi

echo ""

# Step 5: Check Emulators
echo -e "${CYAN}[EMULATORS]${NC}"

COSMOS_STATUS=$(docker ps --filter "name=cosmos-emulator" --format "{{.Status}}" 2>/dev/null)
if [[ -n "$COSMOS_STATUS" ]]; then
    if [[ "$COSMOS_STATUS" == *"healthy"* ]]; then
        echo -e "  ${GREEN}Cosmos DB Emulator: ${COSMOS_STATUS}${NC}"
        ((PASS_COUNT++))
    else
        echo -e "  ${YELLOW}Cosmos DB Emulator: ${COSMOS_STATUS}${NC}"
        ((WARN_COUNT++))
    fi
else
    echo -e "  ${RED}Cosmos DB Emulator: Not running${NC}"
    echo -e "    ${GRAY}Start with: docker compose --profile infra up -d cosmos-emulator${NC}"
    ((FAIL_COUNT++))
fi

RABBITMQ_STATUS=$(docker ps --filter "name=rabbitmq" --format "{{.Status}}" 2>/dev/null)
if [[ -n "$RABBITMQ_STATUS" ]]; then
    if [[ "$RABBITMQ_STATUS" == *"healthy"* || "$RABBITMQ_STATUS" == Up* ]]; then
        echo -e "  ${GREEN}RabbitMQ: ${RABBITMQ_STATUS}${NC}"
        ((PASS_COUNT++))
    else
        echo -e "  ${YELLOW}RabbitMQ: ${RABBITMQ_STATUS}${NC}"
        ((WARN_COUNT++))
    fi
else
    echo -e "  ${RED}RabbitMQ: Not running${NC}"
    echo -e "    ${GRAY}Start with: docker compose --profile infra up -d rabbitmq${NC}"
    ((FAIL_COUNT++))
fi

echo ""

# Step 6: Issues Detection
CRASHED=$(docker ps -a --filter "name=riskinsure" --filter "status=exited" --format "{{.Names}}: {{.Status}}")
if [[ -n "$CRASHED" ]]; then
    echo -e "${CYAN}[ISSUES DETECTED]${NC}"
    echo "$CRASHED" | while IFS= read -r line; do
        echo -e "  ${YELLOW}${line}${NC}"
    done
    echo ""
    echo -e "${CYAN}[NEXT STEPS]${NC}"
    echo -e "  ${GRAY}1. Check logs: docker logs <container-name>${NC}"
    echo -e "  ${GRAY}2. Restart specific service: docker compose restart <service-name>${NC}"
    echo -e "  ${GRAY}3. Restart all services: docker compose restart${NC}"
    echo ""
fi

# Step 7: Overall Result
echo -e "${CYAN}[OVERALL RESULT]${NC}"

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

if [[ $FAIL_COUNT -eq 0 && $RUNNING_COUNT -eq 10 && $API_PASS_COUNT -eq 5 ]]; then
    echo -e "${GREEN}PASS${NC} - All services operational"
    EXIT_CODE=0
elif [[ $FAIL_COUNT -le 2 && $RUNNING_COUNT -ge 8 ]]; then
    echo -e "${YELLOW}PARTIAL PASS${NC} - ${RUNNING_COUNT}/10 services running, ${API_PASS_COUNT}/5 APIs responding"
    EXIT_CODE=0
else
    echo -e "${RED}FAIL${NC} - Critical services down (${RUNNING_COUNT}/10 containers, ${API_PASS_COUNT}/5 APIs)"
    EXIT_CODE=1
fi

echo ""
echo -e "${GRAY}Execution time: ${DURATION} seconds${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

exit $EXIT_CODE

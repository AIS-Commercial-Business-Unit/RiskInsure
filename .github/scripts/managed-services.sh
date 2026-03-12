#!/usr/bin/env bash
set -euo pipefail

MANAGED_SERVICES=(
  billing
  customer
  customerrelationshipsmgt
  fundstransfermgt
  modernizationpatterns
  policy
  policylifecyclemgt
  policyequityandinvoicingmgt
  ratingandunderwriting
  riskratingandunderwriting
)

managed_services_json() {
  printf '%s\n' "${MANAGED_SERVICES[@]}" | jq -R . | jq -s -c .
}

validate_services_json() {
  local services_json="${1:-}"
  local allowed_json
  allowed_json="$(managed_services_json)"

  if ! echo "$services_json" | jq -e 'type == "array" and all(.[]; type == "string" and length > 0)' >/dev/null; then
    echo "Invalid services payload. Expected a JSON string array." >&2
    return 1
  fi

  local invalid_services
  invalid_services=$(echo "$services_json" | jq -r --argjson allowed "$allowed_json" '
    map(select((. as $service | $allowed | index($service)) == null))
    | join(",")
  ')

  if [[ -n "$invalid_services" ]]; then
    echo "Unsupported service(s): $invalid_services" >&2
    echo "Allowed services: $allowed_json" >&2
    return 1
  fi
}

validate_matrix_consistency() {
  local services_json="${1:-}"
  local matrix_empty="${2:-}"

  if [[ "$matrix_empty" != "true" && "$matrix_empty" != "false" ]]; then
    echo "matrix_empty must be 'true' or 'false'." >&2
    return 1
  fi

  local service_count
  service_count=$(echo "$services_json" | jq -r 'length')

  if [[ "$matrix_empty" == "true" && "$service_count" -ne 0 ]]; then
    echo "matrix_empty=true is inconsistent with non-empty services list." >&2
    return 1
  fi

  if [[ "$matrix_empty" == "false" && "$service_count" -eq 0 ]]; then
    echo "matrix_empty=false is inconsistent with an empty services list." >&2
    return 1
  fi
}

parse_dispatch_services_input() {
  local raw_input="${1:-}"

  if [[ -z "$raw_input" ]]; then
    echo "The services input is required." >&2
    return 1
  fi

  local normalized
  normalized=$(echo "$raw_input" | tr '[:upper:]' '[:lower:]')

  if [[ "$normalized" == "all" ]]; then
    managed_services_json
    return 0
  fi

  local services_json
  services_json=$(printf '%s' "$raw_input" | jq -R -s -c '
    split(",")
    | map(gsub("^\\s+|\\s+$"; ""))
    | map(ascii_downcase)
    | map(select(length > 0))
    | reduce .[] as $service ([]; if index($service) then . else . + [$service] end)
  ')

  if [[ "$(echo "$services_json" | jq -r 'length')" -eq 0 ]]; then
    echo "The services input did not contain any valid service names." >&2
    return 1
  fi

  validate_services_json "$services_json"
  echo "$services_json"
}

detect_services_from_changed_files() {
  local changed_files="${1:-}"
  local changed_services=()

  for service in billing customer customerrelationshipsmgt fundstransfermgt policy policylifecyclemgt policyequityandinvoicingmgt ratingandunderwriting riskratingandunderwriting; do
    if echo "$changed_files" | grep -q "^services/$service/"; then
      changed_services+=("$service")
    fi
  done

  if echo "$changed_files" | grep -q "^platform/modernizationpatterns/Api/"; then
    changed_services+=("modernizationpatterns")
  fi

  if [[ ${#changed_services[@]} -eq 0 ]]; then
    echo '[]'
  else
    printf '%s\n' "${changed_services[@]}" | jq -R . | jq -s -c .
  fi
}

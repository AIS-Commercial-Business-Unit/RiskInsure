# Plan: Fix GitHub Actions Code Injection (CodeQL Critical)

## Summary
CodeQL flagged 9 critical code-injection vulnerabilities in `cd-services-dev.yml`. All stem from `${{ expression }}` interpolation directly inside `run:` shell scripts — GitHub Actions evaluates these expressions BEFORE passing the script to bash, so a malicious value exfiltrates secrets or runs arbitrary commands. The fix is: assign to `env:` block → reference via `$VAR` in shell. `ci-build-services.yml` has the same structural pattern (not yet flagged). `cd-infra-dev.yml` is clean — only `choice`-type inputs, no free-text user data.

## Affected Files & Injection Points

### cd-services-dev.yml — MUST FIX (9 CodeQL-flagged + 2 root sources)

**Step: `Decide services + image_tag`** (root sources, not flagged individually):
- `${{ github.event.inputs.image_tag }}` — free-text user input → assigned to `IMAGE_TAG_RAW`
- `${{ github.event.inputs.services }}` — free-text user input → assigned to `services_input`
- `${{ github.event_name }}` — used in if condition

**Step: `Summary` in `determine` job** (lines 162–183, CodeQL-flagged):
- `${{ steps.out.outputs.image_tag }}`, `${{ steps.out.outputs.services }}`, `${{ steps.out.outputs.matrix_empty }}`, `${{ steps.out.outputs.trigger_reason }}` in echo statements
- `${{ github.event_name }}`, `${{ github.event.workflow_run.name/conclusion/id }}` in conditional + echo

**Step: `Build Terraform Targets`** (line ~359, CodeQL-flagged):
- `SERVICES='${{ needs.determine.outputs.services }}'` — inline shell assignment

**Step: `Terraform Plan`** (line ~419, CodeQL-flagged):
- `-var="image_tag=${{ needs.determine.outputs.image_tag }}"` — embedded in terraform command

**Step: `Generate Summary`** (lines ~449–456, CodeQL-flagged):
- `${{ needs.determine.outputs.image_tag/services/matrix_empty/trigger_reason }}`
- `${{ needs.determine.result }}`, `${{ needs.verify-infra.result }}`, `${{ needs.verify-images.result }}`, `${{ needs.deploy.result }}`

### ci-build-services.yml — PREEMPTIVE FIX RECOMMENDED (not yet flagged)

**Step: `Determine Changed Services`**:
- `services_input="${{ github.event.inputs.services }}"` — free-text root injection

**Step: `Summary` in `determine-services` job**:
- `${{ steps.determine.outputs.image_tag/services/matrix_empty/trigger_reason/infra_changed }}` in echo

**Step: `publish-cd-metadata`**:
- Heredoc using `${{ needs.determine-services.outputs.* }}` — unquoted heredoc would expand subshells
- Fix: use `jq --arg`/`--argjson` to construct JSON safely

### cd-infra-dev.yml — NO CHANGES NEEDED
- All `workflow_dispatch` inputs are `choice` type (predefined options, not free-text)
- No user-controlled values flow into `run:` scripts
- Terraform plan steps use only `${{ env.ENVIRONMENT }}` (static) and OIDC secrets

## Fix Pattern

### Replace in `run:` blocks
```
BEFORE:  some_var="${{ needs.foo.outputs.bar }}"
AFTER:   # move to env: block, then use $SOME_VAR in shell
```

### Template for each step

```yaml
- name: Some step
  env:
    SOME_VAR: ${{ needs.foo.outputs.bar }}   # env: block gets raw value safely
  run: |
    echo "$SOME_VAR"  # reference via $VAR, not ${{ }}
```

## Specific Changes

### cd-services-dev.yml

1. **`Decide services + image_tag` step — add `env:` block**:
   ```yaml
   env:
     GH_EVENT_NAME: ${{ github.event_name }}
     INPUT_IMAGE_TAG: ${{ github.event.inputs.image_tag }}
     INPUT_SERVICES: ${{ github.event.inputs.services }}
   ```
   Replace in shell: `"${{ github.event_name }}"` → `"$GH_EVENT_NAME"`, etc.

2. **`Summary` step in `determine` job — add `env:` block**:
   ```yaml
   env:
     GH_EVENT_NAME: ${{ github.event_name }}
     WF_RUN_NAME: ${{ github.event.workflow_run.name }}
     WF_RUN_CONCLUSION: ${{ github.event.workflow_run.conclusion }}
     WF_RUN_ID: ${{ github.event.workflow_run.id }}
     OUT_IMAGE_TAG: ${{ steps.out.outputs.image_tag }}
     OUT_SERVICES: ${{ steps.out.outputs.services }}
     OUT_MATRIX_EMPTY: ${{ steps.out.outputs.matrix_empty }}
     OUT_TRIGGER_REASON: ${{ steps.out.outputs.trigger_reason }}
   ```

3. **`Build Terraform Targets` step — move to `env:` block**:
   Remove `SERVICES='${{ needs.determine.outputs.services }}'` from `run:`.
   Add `env:` block: `SERVICES: ${{ needs.determine.outputs.services }}`
   (matches the existing pattern in `verify-images` step which already does this correctly)

4. **`Terraform Plan` step — add `IMAGE_TAG` env var**:
   ```yaml
   env:
     IMAGE_TAG: ${{ needs.determine.outputs.image_tag }}
     # existing ARM_* vars stay
   ```
   Replace `-var="image_tag=${{ needs.determine.outputs.image_tag }}"` → `-var="image_tag=$IMAGE_TAG"`
   Note: `${{ steps.targets.outputs.targets }}` (Terraform targets) is not user-controlled (case statement output); can also move to env var for consistency as `TF_TARGETS`.

5. **`Generate Summary` step — add full `env:` block**:
   ```yaml
   env:
     IMAGE_TAG: ${{ needs.determine.outputs.image_tag }}
     SERVICES: ${{ needs.determine.outputs.services }}
     MATRIX_EMPTY: ${{ needs.determine.outputs.matrix_empty }}
     TRIGGER_REASON: ${{ needs.determine.outputs.trigger_reason }}
     DETERMINE_RESULT: ${{ needs.determine.result }}
     VERIFY_INFRA_RESULT: ${{ needs.verify-infra.result }}
     VERIFY_IMAGES_RESULT: ${{ needs.verify-images.result }}
     DEPLOY_RESULT: ${{ needs.deploy.result }}
   ```

### ci-build-services.yml

6. **`Determine Changed Services` step — add `env:` block**:
   ```yaml
   env:
     GH_EVENT_NAME: ${{ github.event_name }}
     GH_SHA: ${{ github.sha }}
     GH_EVENT_BEFORE: ${{ github.event.before }}
     INPUT_SERVICES: ${{ github.event.inputs.services }}
   ```
   Replace all four inline `${{ }}` references in the `run:` script with `$GH_EVENT_NAME`, `$GH_SHA`, `$GH_EVENT_BEFORE`, `$INPUT_SERVICES`.

7. **`Summary` step in `determine-services` job — add `env:` block**:
   ```yaml
   env:
     GH_EVENT_NAME: ${{ github.event_name }}
     GH_SHA: ${{ github.sha }}
     OUT_IMAGE_TAG: ${{ steps.determine.outputs.image_tag }}
     OUT_SERVICES: ${{ steps.determine.outputs.services }}
     OUT_MATRIX_EMPTY: ${{ steps.determine.outputs.matrix_empty }}
     OUT_INFRA_CHANGED: ${{ steps.determine.outputs.infra_changed }}
     OUT_TRIGGER_REASON: ${{ steps.determine.outputs.trigger_reason }}
   ```

8. **`publish-cd-metadata` step — replace unquoted heredoc with `jq`** (unquoted `<<EOF` heredoc expands `$(...)` subshells, making embedded `${{ }}` dangerous):
   ```yaml
   env:
     IMAGE_TAG: ${{ needs.determine-services.outputs.image_tag }}
     SERVICES: ${{ needs.determine-services.outputs.services }}
     MATRIX_EMPTY: ${{ needs.determine-services.outputs.matrix_empty }}
     TRIGGER_REASON: ${{ needs.determine-services.outputs.trigger_reason }}
   run: |
     set -euo pipefail
     mkdir -p .cd-metadata
     jq -n \
       --arg image_tag "$IMAGE_TAG" \
       --argjson services "$SERVICES" \
       --argjson matrix_empty "$([ "$MATRIX_EMPTY" = 'true' ] && echo 'true' || echo 'false')" \
       --arg trigger_reason "$TRIGGER_REASON" \
       '{"image_tag":$image_tag,"services":$services,"matrix_empty":$matrix_empty,"trigger_reason":$trigger_reason}' \
       > .cd-metadata/deployment-inputs.json
     cat .cd-metadata/deployment-inputs.json
   ```

9. **`Generate Summary` step in `summary` job — add `env:` block**:
   ```yaml
   env:
     GH_SHA: ${{ github.sha }}
     IMAGE_TAG: ${{ needs.determine-services.outputs.image_tag }}
     SERVICES: ${{ needs.determine-services.outputs.services }}
     MATRIX_EMPTY: ${{ needs.determine-services.outputs.matrix_empty }}
     TRIGGER_REASON: ${{ needs.determine-services.outputs.trigger_reason }}
     BUILD_RESULT: ${{ needs.build.result }}
     METADATA_RESULT: ${{ needs.publish-cd-metadata.result }}
   ```

## Cross-Workflow Impact

- **NO breaking changes**: fixes are purely internal to each workflow's shell scripts
- The `cd-inputs` JSON artifact interface between CI and CD is unchanged
- Terraform commands remain functionally identical
- The `verify-images` step in `cd-services-dev.yml` already correctly uses `env:` block pattern — new changes mirror that existing pattern

## Verification
1. Run `cd-services-dev.yml` via `workflow_dispatch` with `services=billing` and a valid image tag — check Summary step shows correct values
2. Run `ci-build-services.yml` via `workflow_dispatch` with `services=billing` — verify artifact JSON is correctly structured
3. After merging, confirm CodeQL scan no longer reports `actions/code-injection/critical` in these files

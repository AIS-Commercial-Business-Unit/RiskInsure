export const LogStyles = {
  reset: '\x1b[0m',
  bold: '\x1b[1m',
  dim: '\x1b[2m',
  cyan: '\x1b[36m',
  yellow: '\x1b[33m',
  green: '\x1b[32m',
} as const;

export function logStep(scope: string, message: string): void {
  console.log(`${LogStyles.bold}${LogStyles.cyan}🔹 [${scope}]${LogStyles.reset} ${message}`);
}

export function logInfo(scope: string, message: string): void {
  console.log(`${LogStyles.yellow}ℹ️  [${scope}]${LogStyles.reset} ${message}`);
}

export function logSuccess(scope: string, message: string): void {
  console.log(`${LogStyles.bold}${LogStyles.green}✅ [${scope}]${LogStyles.reset} ${message}`);
}

export function logDetail(message: string): void {
  console.log(`${LogStyles.dim}${message}${LogStyles.reset}`);
}

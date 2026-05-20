const requireEnv = (key: string, fallback?: string) => {
  const value = import.meta.env[key] as string | undefined;
  if (value) {
    return value;
  }
  if (fallback !== undefined) {
    return fallback;
  }
  throw new Error(`Missing required env: ${key}`);
};

export const env = {
  appEnv: requireEnv("VITE_APP_ENV", "development"),
  apiBaseUrl: requireEnv("VITE_API_BASE_URL", "http://localhost:5000")
};

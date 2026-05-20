import { useEffect, useState } from "react";

type WeatherForecast = {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
};

export default function App() {
  const [data, setData] = useState<WeatherForecast[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    const baseUrl = import.meta.env.VITE_API_GATEWAY_URL as string | undefined;
    if (!baseUrl) {
      setError("VITE_API_GATEWAY_URL is not set");
      return () => controller.abort();
    }

    fetch(`${baseUrl}/api/auth/weatherforecast`, { signal: controller.signal })
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Request failed: ${response.status}`);
        }
        return response.json();
      })
      .then((payload: WeatherForecast[]) => setData(payload))
      .catch((err: Error) => {
        if (err.name !== "AbortError") {
          setError(err.message);
        }
      });

    return () => controller.abort();
  }, []);

  return (
    <main style={{ padding: "2rem", fontFamily: "Arial, sans-serif" }}>
      <h1>DevPrep Smoke Test</h1>
      {error && <p style={{ color: "crimson" }}>{error}</p>}
      {!error && !data && <p>Loading...</p>}
      {data && (
        <pre style={{ whiteSpace: "pre-wrap" }}>{JSON.stringify(data, null, 2)}</pre>
      )}
    </main>
  );
}

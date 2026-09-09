import type { NextConfig } from "next";

const apiProxyUrl = process.env.API_PROXY_URL?.trim().replace(/\/+$/, "");

const nextConfig: NextConfig = {
  // When API_PROXY_URL is set (e.g. http://localhost:7000), forward same-origin
  // /api/* requests to the backend so the browser never makes a cross-origin
  // call — no CORS setup and cookies just work. Without it, no rewrite is
  // installed and /api/* is left untouched.
  ...(apiProxyUrl
    ? {
        async rewrites() {
          return [
            {
              source: "/api/:path*",
              destination: `${apiProxyUrl}/api/:path*`,
            },
          ];
        },
      }
    : {}),
};

export default nextConfig;

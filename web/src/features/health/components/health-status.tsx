"use client";

import { useQuery } from "@tanstack/react-query";
import { CircleAlertIcon } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { getErrorMessage } from "@/lib/api/errors";

import { healthQuery } from "../queries";

/**
 * Example consumer of the full data flow:
 * useQuery → feature query → feature API service → lib/api client → backend.
 */
export function HealthStatus() {
  const { data, isPending, isError, error, refetch } = useQuery(healthQuery);

  if (isPending) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Spinner /> Checking API health…
      </div>
    );
  }

  if (isError) {
    return (
      <Alert variant="destructive">
        <CircleAlertIcon />
        <AlertTitle>API unreachable</AlertTitle>
        <AlertDescription>{getErrorMessage(error)}</AlertDescription>
        <Button variant="outline" size="xs" onClick={() => refetch()}>
          Retry
        </Button>
      </Alert>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          API health
          <Badge variant={data.status === "Healthy" ? "default" : "destructive"}>
            {data.status}
          </Badge>
        </CardTitle>
        <CardDescription>GET /api/health via the API client.</CardDescription>
      </CardHeader>
      {data.checks ? (
        <CardContent className="space-y-1">
          {data.checks.map((check) => (
            <div key={check.name} className="flex items-center justify-between text-sm">
              <span>{check.name}</span>
              <span className="text-muted-foreground">{check.status}</span>
            </div>
          ))}
        </CardContent>
      ) : null}
    </Card>
  );
}

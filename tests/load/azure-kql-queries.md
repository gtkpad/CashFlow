# KQL Queries for NFR Validation

Queries for Azure Portal (Application Insights > Logs). Adjust `ago(10m)` to match your test window.

## 1. Latency by endpoint during load test

```kql
requests
| where timestamp > ago(10m)
| where name has "consolidation" or name has "transactions"
| summarize p50=percentile(duration, 50), p95=percentile(duration, 95), p99=percentile(duration, 99), count=count() by name, bin(timestamp, 30s)
| order by timestamp asc
```

## 2. Custom metrics (transactions created, summaries updated)

```kql
customMetrics
| where timestamp > ago(10m)
| where name in ("cashflow.transactions.created", "cashflow.daily_summary.updated", "cashflow.consolidation.queries")
| summarize total=sum(value) by name, bin(timestamp, 30s)
| order by timestamp asc
```

## 3. NFR-2 validation (consolidation p95)

```kql
requests
| where timestamp > ago(10m)
| where name has "consolidation"
| summarize p95_ms=percentile(duration, 95), avg_ms=avg(duration), req_count=count(), error_rate=countif(success == false) * 100.0 / count()
| extend nfr2_pass = p95_ms < 200
```

## 4. NFR-4 validation (transactions p95)

```kql
requests
| where timestamp > ago(10m)
| where name has "transactions" and name has "POST"
| summarize p95_ms=percentile(duration, 95), avg_ms=avg(duration), req_count=count(), error_rate=countif(success == false) * 100.0 / count()
| extend nfr4_pass = p95_ms < 500
```

## 5. Overall throughput by service

```kql
requests
| where timestamp > ago(10m)
| summarize rps=count() / 600.0, errors=countif(success == false) by cloud_RoleName
| order by rps desc
```

## 6. Health check status during test

```kql
requests
| where timestamp > ago(10m)
| where name has "health"
| summarize status_ok=countif(resultCode == "200"), status_fail=countif(resultCode != "200") by cloud_RoleName, bin(timestamp, 1m)
```

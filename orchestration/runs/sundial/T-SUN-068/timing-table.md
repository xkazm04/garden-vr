# Windows player latency, backend=cli

20 sequential calls, 10 per kind; two preceding live UI calls excluded. One install key, starts at least 11 seconds apart.
Elapsed time covers SundialCoach/CoachFlow, UnityWebRequest, guards, and the player's completion observation (frame granularity). Budget: 15 seconds.
p50 is the median (average of the middle pair); p95 uses nearest rank. Includes every call, including fallbacks.

| Kind | n | Model replies | p50 ms | p95 ms |
|---|---:|---:|---:|---:|
| All | 20 | 18 | 4658.616 | 6333.906 |
| Onboarding | 10 | 10 | 4667.003 | 5200.590 |
| Reflection | 10 | 8 | 4600.273 | 7167.508 |

| Sample | Kind | Player ms | Relay ms | From model | Failure |
|---:|---|---:|---:|---|---|
| 1 | Onboarding | 4650.331 | 4610 | true | None |
| 2 | Reflection | 4566.878 | 4521 | true | None |
| 3 | Onboarding | 4650.415 | 4597 | true | None |
| 4 | Reflection | 4267.118 | 4221 | true | None |
| 5 | Onboarding | 5000.590 | 4956 | true | None |
| 6 | Reflection | 4800.458 | 4763 | true | None |
| 7 | Onboarding | 3799.582 | 3749 | true | None |
| 8 | Reflection | 4550.543 | 4509 | true | None |
| 9 | Onboarding | 4933.665 | 4895 | true | None |
| 10 | Reflection | 7167.508 | 7121 | false | Error |
| 11 | Onboarding | 4267.733 | 4218 | true | None |
| 12 | Reflection | 3901.214 | 3853 | false | Error |
| 13 | Onboarding | 4666.817 | 4618 | true | None |
| 14 | Reflection | 4633.669 | 4592 | true | None |
| 15 | Onboarding | 4667.190 | 4617 | true | None |
| 16 | Reflection | 6333.906 | 6297 | true | None |
| 17 | Onboarding | 5200.590 | 5164 | true | None |
| 18 | Reflection | 5534.168 | 5486 | true | None |
| 19 | Onboarding | 4833.840 | 4787 | true | None |
| 20 | Reflection | 3784.636 | 3742 | true | None |

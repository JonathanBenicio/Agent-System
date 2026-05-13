---
description: Run the master CLI security extension scanner on the project to audit secrets, credentials, and dependency risks.
---

# /security-gate - Interactive Code Security Audit

$ARGUMENTS

---

## 🔴 CRITICAL RULES

1. **Gatekeeper Priority**: This command runs the `gemini-cli-extensions/security` tool. No build or deploy is permitted if it reports critical findings.
2. **Audit All Layers**: Scan credentials, environmental secrets, third-party packages, and sensitive endpoints.
3. **Report Immediately**: Provide a concise summary of all critical, high, medium, and low issues detected.

---

## Task

Execute the security scan using the `security-auditor` specialist agent and the `vulnerability-scanner` skill with this context:

```
CONTEXT:
- Target Directory: Current Workspace (.)
- Extension Tool: gemini-cli-extensions/security
- Mode: INTERACTIVE SECURITY GATEWAY

RULES:
1. Trigger the master CLI security scan over the entire repository.
2. Intercept and parse any exit codes or scan output.
3. Classify all vulnerabilities found into severity bands (Critical, High, Medium, Low).
4. If a critical secret or key is found leaked, immediately flag it and block further automated deployment actions.
```

---

## Expected Output

| Deliverable | Location | Description |
|-------------|----------|-------------|
| Security Gate Report | Interactive Shell Output | Clear listing of vulnerabilities, exposed credentials, and compliance violations. |
| Remediation Steps | Interactive Shell Output | Actionable guidelines to fix or patch each reported security risk. |

---

## After Audit

If the audit is clean:
```
[OK] Security Gate passed successfully!
No critical/high vulnerabilities or exposed secrets detected.
```

If issues are found:
```
[WARNING] Security Gate detected active vulnerabilities!
Please review and fix the highlighted security warnings before proceeding to deployment.
```

---

## Usage

```
/security-gate
/security-gate --verbose
```

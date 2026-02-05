# Known Security Issues

This document tracks known security vulnerabilities that cannot be immediately fixed and documents their mitigation strategies.

## Active Vulnerabilities

### DotNetZip 1.16.0 - Directory Traversal (CVE-2024-48510)

**Severity:** High (CVSS 9.8)  
**Advisory:** [GHSA-xhg6-9j5j-w4vf](https://github.com/advisories/GHSA-xhg6-9j5j-w4vf)  
**Status:** Accepted Risk  
**Date Identified:** 2026-02-05

#### Description
DotNetZip 1.16.0 contains a directory traversal vulnerability that could allow remote code execution through crafted ZIP archives. The library is no longer maintained, and no patched version exists.

#### Why We Can't Fix It
- DotNetZip is a **transitive dependency** from QuantConnect.Lean (version 2.5.17279)
- DotNetZip has no newer version available (1.16.0 is the latest)
- The library is unmaintained
- QuantConnect.Lean has not yet migrated to a secure alternative

#### Risk Assessment
**Impact:** Low in our context  
**Likelihood:** Very Low

**Rationale:**
1. The vulnerability requires processing untrusted ZIP files from external sources
2. Our application primarily:
   - Collects and stores market data from trusted APIs
   - Does not accept ZIP file uploads from users
   - Does not extract ZIP files from untrusted sources
3. QuantConnect.Lean integration is used for backtesting algorithms, not production data processing
4. The vulnerability path is not exposed in normal application usage

#### Mitigation Measures

**Current Mitigations:**
- Application does not accept ZIP file uploads from users
- No functionality that processes ZIP files from external/untrusted sources
- QuantConnect integration is isolated from user input processing
- Security workflow updated to explicitly track this vulnerability

**Future Plans:**
1. Monitor QuantConnect.Lean releases for dependency updates
2. Consider migrating to alternative backtesting frameworks if QuantConnect doesn't address this
3. Evaluate if QuantConnect integration can be made optional/plugin-based
4. Contribute to QuantConnect.Lean to help migrate to System.IO.Compression or ProDotNetZip

#### Alternatives Considered

**Option 1: Override with ProDotNetZip**  
Status: Not viable - NuGet package resolution doesn't support substituting different packages for transitive dependencies

**Option 2: Remove QuantConnect.Lean**  
Status: Not chosen - Lean integration provides valuable backtesting capabilities

**Option 3: Wait for QuantConnect.Lean update**  
Status: Current approach - monitoring upstream for fixes

#### References
- [CVE-2024-48510 Details](https://nvd.nist.gov/vuln/detail/CVE-2024-48510)
- [DotNetZip Security Advisory](https://github.com/advisories/GHSA-xhg6-9j5j-w4vf)
- [ProDotNetZip (Secure Alternative)](https://www.nuget.org/packages/ProDotNetZip/)
- [Related PR: Security Vulnerability Fixes](../../pulls)

#### Monitoring
This vulnerability is automatically checked in the Security CI workflow and will be re-evaluated:
- On each QuantConnect.Lean release
- Monthly security review
- When new exploits or attack vectors are discovered

---

## Previously Fixed Vulnerabilities

### System.Drawing.Common 4.7.0 - RCE (CVE-2021-24112)
**Fixed:** 2026-02-05  
**Resolution:** Updated to version 9.0.0 via Directory.Packages.props override

### System.Net.Security 4.3.0 - Multiple Vulnerabilities
**Fixed:** 2026-02-05  
**Resolution:** Updated to version 4.3.2 via Directory.Packages.props override

### System.Private.ServiceModel 4.4.0 - High Severity
**Fixed:** 2026-02-05  
**Resolution:** Updated to version 4.10.3 via Directory.Packages.props override

### System.ServiceModel.Primitives 4.4.0 - High Severity
**Fixed:** 2026-02-05  
**Resolution:** Updated to version 4.10.3 via Directory.Packages.props override

### System.Formats.Asn1 6.0.0 - High Severity
**Fixed:** 2026-02-05  
**Resolution:** Updated to version 9.0.1 via Directory.Packages.props override

### System.Security.Cryptography.Pkcs 6.0.1 - High Severity
**Fixed:** 2026-02-05  
**Resolution:** Updated to version 9.0.1 via Directory.Packages.props override

---

**Last Updated:** 2026-02-05  
**Review Cycle:** Monthly or upon major dependency updates

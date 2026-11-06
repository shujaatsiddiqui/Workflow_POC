# Elsa Workflows 3.9.0 - Payment Validation POC

## Overview

This is a proof of concept for **Elsa Workflows 3.9.0** demonstrating a payment validation workflow for the SFS (Student Financial Services) domain. The POC shows how to create custom activities that encapsulate complex business logic while presenting a simple drag-and-drop interface to business users.

## Architecture

### Technology Stack
- **.NET 10.0** - Application framework
- **Elsa Workflows 3.9.0** - Workflow engine
- **Elsa Studio** - Visual workflow designer (Docker)
- **Microsoft RulesEngine 6.0.1** - JSON-based business rules
- **In-memory storage** - Workflow persistence (temporary)

### Components

```
┌─────────────────────────────────────────────────────┐
│  Elsa Studio (Port 14000)                          │
│  - Visual workflow designer                         │
│  - Drag-and-drop activity canvas                   │
└─────────────────┬───────────────────────────────────┘
                  │ HTTP/SignalR
┌─────────────────▼───────────────────────────────────┐
│  Elsa Server (Port 5001)                           │
│  ┌──────────────────────────────────────────────┐  │
│  │ Custom Activities (SFS Category)             │  │
│  │  - CheckDuplicateActivity                    │  │
│  │  - EnrichAccountActivity                     │  │
│  │  - ValidatePaymentActivity                   │  │
│  │  - SfsValidatePaymentActivity (RulesEngine)  │  │
│  └──────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────┐  │
│  │ Services                                     │  │
│  │  - SfsPaymentValidationService               │  │
│  │  - Microsoft RulesEngine                     │  │
│  └──────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────┐  │
│  │ Business Rules (JSON)                        │  │
│  │  - Rules/ValidationRules.json                │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

## Setup Instructions

### Prerequisites
- .NET 10 SDK
- Docker Desktop
- Git
- VS Code (optional)

### Step 1: Run Elsa Studio (Docker)
```bash
docker run -d \
  --name elsa-studio \
  -p 14000:80 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e Elsa__Server__Url=http://localhost:5001/elsa/api \
  elsaworkflows/elsa-studio-v3:latest
```

### Step 2: Configure User Secrets
```bash
cd ElsaServer
dotnet user-secrets init
dotnet user-secrets set "Identity:Tokens:SigningKey" "your-super-secret-key-min-32-chars"
dotnet user-secrets set "Identity:Bootstrap:UserName" "admin"
dotnet user-secrets set "Identity:Bootstrap:Password" "Admin@12345"
```

### Step 3: Run Elsa Server
```bash
dotnet run --urls=http://localhost:5001
```

### Step 4: Access Studio
1. Open browser: http://localhost:14000
2. Login with: `admin` / `Admin@12345`

## Payment Validation Workflow Example

### Business Problem

Process student payments with complex validation rules:
- Prevent duplicate payments
- Validate account status and balance
- Apply business rules based on account state
- Route invalid payments for manual review

### Solution: Multi-Activity Workflow

We created **3 custom activities** to solve this problem, demonstrating how complex business logic can be hidden behind simple, reusable workflow components.

---

## Custom Activities

### 1. CheckDuplicateActivity

**Purpose:** Prevent duplicate payment processing

**Category:** SFS → Payment Processing
**Display Name:** CheckDuplicate

**Inputs:**
- `PaymentId` (string) - Payment identifier
- `Amount` (decimal) - Payment amount

**Outputs:**
- `IsDuplicate` (bool) - True if payment already posted
- `Message` (string) - Duplicate check result message

**Custom Outcomes:**
- `Duplicate` - Payment found in posted payments
- `NotDuplicate` - Payment is unique, proceed

**Mock Data:**
```csharp
PostedPayments = {
    "PAY-001:1000",
    "PAY-005:500"
}
```

**Example Usage:**
```
Start
  → CheckDuplicate (PaymentId: "PAY-002", Amount: 500)
      → Duplicate → WriteLine("Payment already posted!")
      → NotDuplicate → Continue processing...
```

---

### 2. EnrichAccountActivity

**Purpose:** Retrieve current account information for validation

**Category:** SFS → Payment Processing
**Display Name:** EnrichAccount

**Inputs:**
- `PaymentId` (string) - Payment identifier

**Outputs:**
- `Balance` (decimal) - Current account balance
- `Status` (int) - Account status code (FIC_CLSFD_CD)
- `ClosedToPosting` (int) - Closed to posting flag (0=open, 1=closed)

**Mock Account Data:**
| PaymentId | Balance | Status | ClosedToPosting | Description |
|-----------|---------|--------|-----------------|-------------|
| PAY-001   | $1,000  | 10     | 0               | Normal account |
| PAY-002   | $500    | 10     | 0               | Normal account |
| PAY-003   | $1,000  | 93     | 0               | Blocked (status 93) |
| PAY-004   | $100    | 10     | 1               | Closed to posting |
| PAY-006   | $0      | 10     | 0               | Zero balance |
| Default   | $1,000  | 10     | 0               | Fallback for unknown IDs |

**Example Usage:**
```
EnrichAccount (PaymentId: "PAY-003")
  → Outputs: Balance=$1,000, Status=93, ClosedToPosting=0
```

---

### 3. ValidatePaymentActivity

**Purpose:** Validate payment against comprehensive business rules

**Category:** SFS → Payment Processing
**Display Name:** ValidatePayment

**Inputs:**
- `PaymentId` (string) - Payment identifier
- `Amount` (decimal) - Payment amount to validate
- `Balance` (decimal) - Current account balance (from EnrichAccount)
- `Status` (int) - Account status code (from EnrichAccount)
- `ClosedToPosting` (int) - Posting flag (from EnrichAccount)

**Outputs:**
- `IsValid` (bool) - True if all validations passed
- `Errors` (List<string>) - List of validation error messages
- `PaymentType` (string) - Payment classification: "Standard", "Payoff", or "Overpayment"

**Custom Outcomes:**
- `Valid` - Payment passed all validations
- `Invalid` - Payment failed one or more validations

**Business Rules:**
1. ✅ Balance must be > $0
2. ✅ Status must NOT be 93 (blocked account)
3. ✅ ClosedToPosting must be 0 (open for posting)
4. ✅ Amount must NOT exceed Balance (no overpayments)

**Payment Type Classification:**
- **Standard:** Amount < Balance
- **Payoff:** Amount == Balance
- **Overpayment:** Amount > Balance (INVALID)

**Example Usage:**
```
ValidatePayment (
    PaymentId: "PAY-003",
    Amount: 500,
    Balance: 1000,
    Status: 93,
    ClosedToPosting: 0
)
  → IsValid: false
  → Errors: ["Account status 93 blocks posting"]
  → PaymentType: "Standard"
  → Outcome: Invalid
```

---

### 4. SfsValidatePaymentActivity (RulesEngine-based)

**Purpose:** Validate payments using configurable JSON rules

**Category:** SFS
**Display Name:** SFS_ValidatePayment

**Inputs:**
- `PaymentId` (string) - Payment identifier
- `RuleSelection` (string) - Which rule to run ("All Rules" or specific rule name)

**Outputs:**
- `IsValid` (bool) - True if payment passed selected rules
- `Errors` (List<string>) - List of rule violations

**JSON Rules:** (from `Rules/ValidationRules.json`)
```json
{
  "BR-005": "Zero Payment Amount",
  "BR-008": "Account status 93 blocks posting"
}
```

**Example Usage:**
```
SfsValidatePayment (
    PaymentId: "PAY-004",
    RuleSelection: "All Rules"
)
  → IsValid: false
  → Errors: ["Zero Payment Amount", "Account status 93 blocks posting"]
```

---

## Complete Workflow Example

### Scenario: Process Multiple Payments with ForEach Loop

**Goal:** Process an array of payments, checking for duplicates, enriching with account data, and validating against business rules.

**Workflow Structure:**
```
Start
  ↓
ForEach (Array of payments)
  ↓
  ├─ CheckDuplicate
  │   ├─ Duplicate → WriteLine("Skipped: Duplicate payment")
  │   └─ NotDuplicate → EnrichAccount
  │                       ↓
  │                     ValidatePayment
  │                       ├─ Valid → WriteLine("Payment ready to post")
  │                       └─ Invalid → WriteLine("Routed to UPQ: [errors]")
```

**Test Data:**
```javascript
[
  { id: "PAY-001", amount: 1000 },  // Duplicate
  { id: "PAY-002", amount: 500 },   // Valid
  { id: "PAY-003", amount: 800 },   // Invalid (Status 93)
  { id: "PAY-004", amount: 50 },    // Invalid (Closed to posting)
  { id: "PAY-006", amount: 100 }    // Invalid (Zero balance)
]
```

**Expected Results:**
| PaymentId | Result | Reason |
|-----------|--------|--------|
| PAY-001   | ⛔ Duplicate | Already posted in system |
| PAY-002   | ✅ Valid | All validations passed |
| PAY-003   | ❌ Invalid | Account status 93 blocks posting |
| PAY-004   | ❌ Invalid | Account is closed to posting |
| PAY-006   | ❌ Invalid | Account balance is zero or negative |

### Studio Configuration Steps

1. **Add ForEach Activity**
   - Items: `[{id:"PAY-001",amount:1000},{id:"PAY-002",amount:500},...]`

2. **Add CheckDuplicate (in ForEach body)**
   - PaymentId: `getOutputFrom("ForEach1", "CurrentValue").id`
   - Amount: `getOutputFrom("ForEach1", "CurrentValue").amount`

3. **Connect Duplicate path → WriteLine**
   - Text: `"Duplicate: " + getOutputFrom("CheckDuplicate1", "Message")`

4. **Connect NotDuplicate path → EnrichAccount**
   - PaymentId: `getOutputFrom("ForEach1", "CurrentValue").id`

5. **Connect EnrichAccount → ValidatePayment**
   - PaymentId: `getOutputFrom("ForEach1", "CurrentValue").id`
   - Amount: `getOutputFrom("ForEach1", "CurrentValue").amount`
   - Balance: `getOutputFrom("EnrichAccount1", "Balance")`
   - Status: `getOutputFrom("EnrichAccount1", "Status")`
   - ClosedToPosting: `getOutputFrom("EnrichAccount1", "ClosedToPosting")`

6. **Connect Valid path → WriteLine**
   - Text: `"Valid payment: " + getOutputFrom("ValidatePayment1", "PaymentType")`

7. **Connect Invalid path → WriteLine**
   - Text: `"Invalid: " + getOutputFrom("ValidatePayment1", "Errors").join(", ")`

---

## Expression Syntax Reference

### Accessing Activity Outputs (JavaScript)
```javascript
// Get single output value
getOutputFrom("ActivityName1", "OutputName")

// Get object properties
getOutputFrom("ForEach1", "CurrentValue").id
getOutputFrom("ForEach1", "CurrentValue").amount

// Join array of strings
getOutputFrom("ValidatePayment1", "Errors").join(", ")

// Conditional expression
getOutputFrom("ValidatePayment1", "IsValid")
  ? "Payment ready"
  : "Payment failed"
```

---

## Project Structure

```
ElsaServer/
├── Activities/
│   ├── CheckDuplicateActivity.cs       # Duplicate detection
│   ├── EnrichAccountActivity.cs        # Account data retrieval
│   ├── ValidatePaymentActivity.cs      # Comprehensive validation
│   └── SfsValidatePaymentActivity.cs   # RulesEngine-based validation
├── Services/
│   ├── IPaymentValidationService.cs    # Service interface
│   └── SfsPaymentValidationService.cs  # RulesEngine implementation
├── Rules/
│   └── ValidationRules.json            # Business rules (JSON)
├── Program.cs                          # Server configuration
├── ElsaServer.csproj                   # Project dependencies
└── README.md                           # This file
```

---

## Key Features Demonstrated

### ✅ Custom Activity Development
- Created reusable activities with custom inputs/outputs
- Implemented custom outcome ports (Duplicate/NotDuplicate, Valid/Invalid)
- Used proper Elsa 3.9 syntax with `[FlowNode]` attribute

### ✅ Business Logic Encapsulation
- Complex validation rules hidden inside activities
- Mock data simulates database lookups
- Business users only see simple input fields

### ✅ Workflow Composition
- Multiple activities chained together
- Conditional branching based on outcomes
- ForEach loop for batch processing

### ✅ Expression Language Integration
- JavaScript expressions for dynamic values
- Access to activity outputs via `getOutputFrom()`
- Array manipulation and conditionals

### ✅ RulesEngine Integration
- JSON-based configurable rules
- Rule selection dropdown (all rules or specific rule)
- Extensible rule definitions

---

## Known Limitations

### ⚠️ In-Memory Storage
- **Issue:** Workflows are stored in memory only
- **Impact:** All workflows lost when server restarts
- **Workaround:** Keep server running during development
- **Root Cause:** Elsa 3.9.0 EF Core persistence API incompatible with .NET 10

### ⚠️ Mock Data
- Activities use hardcoded mock data
- Real implementation would query actual databases/APIs

---

## Future Enhancements

### 🔧 Persistence
- Downgrade to Elsa 3.3.x (known working persistence)
- OR wait for Elsa 3.10+ with .NET 10 support
- OR use in-memory + workflow export/import

### 🔧 Real Data Integration
- Connect EnrichAccount to actual student database
- Integrate CheckDuplicate with payment history table
- Load business rules from configuration database

### 🔧 Additional Activities
- **PostPaymentActivity** - Record payment transaction
- **Send509AccountActivity** - Route to specific account types
- **GenerateReceiptActivity** - Create payment receipt
- **NotifyStudentActivity** - Send confirmation email

### 🔧 Error Handling
- Retry logic for transient failures
- Dead letter queue for failed payments
- Alerting for validation rule violations

---

## Technical Notes

### Elsa 3.9.0 Syntax Patterns

**Activity Declaration:**
```csharp
[Activity(
    Namespace = "SFS",
    DisplayName = "ActivityName",
    Description = "What this activity does")]
[FlowNode("Outcome1", "Outcome2")]  // For custom outcomes
public class MyActivity : Activity
```

**Input/Output Properties:**
```csharp
[Input(DisplayName = "Label", Description = "Help text")]
public Input<string> MyInput { get; set; } = default!;

[Output(Description = "Output description")]
public Output<bool> MyOutput { get; set; } = default!;
```

**Accessing Values:**
```csharp
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    // Get input
    var value = context.Get(MyInput);

    // Set output
    context.Set(MyOutput, true);

    // Complete with outcome
    await context.CompleteActivityWithOutcomesAsync("Outcome1");
}
```

---

## Troubleshooting

### Studio Can't Connect to Server
- Check server is running on port 5001
- Verify CORS is enabled in Program.cs
- Check Studio environment variable: `Elsa__Server__Url=http://localhost:5001/elsa/api`

### Activities Not Appearing in Toolbox
- Verify activity has `[Activity]` attribute with correct namespace
- Check `elsa.AddActivitiesFrom<Program>()` in Program.cs
- Restart server and hard refresh Studio (Cmd+Shift+R)

### Custom Outcomes Not Showing as Ports
- Verify `[FlowNode("Outcome1", "Outcome2")]` attribute
- Check using `CompleteActivityWithOutcomesAsync()` not `CompleteActivityAsync()`
- Import correct namespace: `using Elsa.Workflows.Activities.Flowchart.Attributes;`

---

## References

- **Elsa Documentation:** https://docs.elsaworkflows.io
- **Elsa GitHub:** https://github.com/elsa-workflows/elsa-core
- **Microsoft RulesEngine:** https://github.com/microsoft/RulesEngine
- **Project Repository:** https://github.com/shujaatsiddiqui/Workflow_POC

---

## Contributors

- Development: Shujaat Siddiqui
- Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>

---

**Last Updated:** November 5, 2026
**Elsa Version:** 3.9.0
**.NET Version:** 10.0

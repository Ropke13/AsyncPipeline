# AsyncPipeline.RobertasTru

A lightweight, extensible, and composable async pipeline library for .NET.  
Chain tasks, add retry logic, validate steps, branch conditionally, or run in parallel — all with clean async support.

---

## ✨ Features

- ✅ Generic async step chaining
- 🔁 Retry logic per step
- ⏱️ Step timeouts
- 🔍 Validation hooks
- 🔄 Conditional branching
- 🧵 Parallel execution
- 🎯 Step start/success/error hooks
- 🧳 Optional pipeline context object
- ✅ Fully unit tested (100% coverage)

---

## 📦 Installation

```bash
dotnet add package AsyncPipeline.RobertasTru


---

## 🚀 Quick Start Example

Basic async chaining:

```csharp
var result = await AsyncPipeline<int, int>
    .Start()
    .Step(async x => x * 2)
    .Step(async x => x + 3)
    .ExecuteAsync(5); // Output: 13
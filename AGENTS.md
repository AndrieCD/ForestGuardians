# Forest Guardians - AI Repository Instructions

> This document defines how AI coding assistants should contribute to this repository.
>
> Before making implementation decisions, also read **PROJECT_CONTEXT.md** located in the repository root.

---

# Project

* Project: Forest Guardians
* Developer: Henia's Roses
* Engine: Unity 6
* Language: C#
* Platform: PC
* Input: Unity New Input System

---

# AI Role

Act as an experienced software engineer joining an existing Unity project.

Your responsibility is to assist with implementation while preserving the project's architecture, maintainability, and coding standards.

You are an implementation assistant—not the project architect or gameplay designer.

Do not redesign systems unless explicitly instructed or there is a significant technical reason.

---

# Engineering Priorities

When making changes, prioritize:

1. Correctness
2. Maintainability
3. Readability
4. Modularity
5. Performance

Never sacrifice maintainability for premature optimization.

---

# Implementation Principles

Implement exactly what is requested.

Do NOT silently introduce:

* new gameplay mechanics
* balance changes
* visual effects
* audio
* polish
* quality-of-life additions
* architectural redesigns

unless explicitly requested or required for correctness.

---

# Project Architecture

General principles:

* Managers coordinate systems.
* Gameplay objects own gameplay.
* UI displays data only.
* ScriptableObjects store configuration.
* MonoBehaviours store runtime state.
* Prefer events over tightly coupled references.
* Prefer composition over inheritance unless there is a true "is-a" relationship.

---

# Coding Standards

Follow the project's coding conventions.

Naming:

* Classes: PascalCase with project prefixes
* Interfaces: I_
* Abstract classes: A_
* MonoBehaviours: Mb_
* ScriptableObjects: *_SO

Fields:

* Public: PascalCase
* SerializeField: PascalCase
* Private: _camelCase
* Constants: ALL_CAPS

Methods:

* PascalCase

General:

* One class per file
* XML documentation for public APIs where appropriate
* Meaningful comments only
* Regions for large classes
* Keep methods focused

---

# Unity Standards

Target:

* Unity 6
* C#
* New Input System

Inspector:

* Expose only designer-facing configuration.
* Keep runtime state private whenever practical.

---

# Implementation Workflow

Before modifying code:

1. Understand the request.
2. Inspect affected systems.
3. Preserve existing architecture.
4. Modify only necessary files.
5. Minimize unrelated edits.
6. Keep implementations production-ready.

---

# Debugging Workflow

When fixing bugs:

1. Identify the intended behavior.
2. Find the root cause.
3. Explain the cause.
4. Apply the smallest reasonable fix.
5. Avoid regressions.

Never guess.

---

# Refactoring Rules

Refactor only when it improves:

* maintainability
* readability
* modularity
* correctness

Avoid stylistic rewrites.

Preserve public APIs whenever possible.

---

# Optimization Rules

Optimization must be evidence-based.

Focus on:

* reducing allocations
* simplifying update loops
* reducing unnecessary work
* improving scalability

Avoid micro-optimizations.

---

# Repository Rules

Do NOT:

* rename files unnecessarily
* reorganize folders without instruction
* introduce new dependencies
* modify unrelated systems
* change gameplay balance
* rewrite large systems without approval

---

# Expected Output

Unless instructed otherwise:

* Produce complete implementations.
* Modify existing code rather than replacing entire systems.
* Preserve compatibility with the current project architecture.
* Assume this repository is an actively developed production codebase.

Before implementing, also read **PROJECT_CONTEXT.md** for project-specific information.
<!-- UNITY CODE ASSIST INSTRUCTIONS START -->
- Project name: ForestGuardians
- Unity version: Unity 6000.3.9f1
- Active game object:
  - Name: PsychicSlamShockwave
  - Tag: Untagged
  - Layer: Projectile
<!-- UNITY CODE ASSIST INSTRUCTIONS END -->
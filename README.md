# custom-malloc

A minimal `malloc`/`free`/`calloc`/`realloc` implementation in C, using an implicit free list and first-fit strategy. Exposed as a shared library for C# interop and tested with xUnit.

## Block Layout

```c
/*
 *  Memory layout of one allocation unit:
 *
 *   ┌──────────────────────┬────────────────────────────┐
 *   │  block_t  (header)   │   usable payload           │
 *   └──────────────────────┴────────────────────────────┘
 *   ^                       ^
 *   block pointer           pointer returned to caller
 */
```

## Features
- Implicit free list, first-fit
- Immediate coalescing
- `sbrk()` heap management
- C# P/Invoke bindings
- xUnit test suite

## Usage
See `custom_malloc.h` for API.

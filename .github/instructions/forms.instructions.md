---
applyTo: "**/*.tsx,**/*.ts"
description: Form standards using React Hook Form + Zod
---

# Form Standards

Stack: React Hook Form + Zod + @hookform/resolvers

## Setup Rules
- Use `zodResolver(schema)` for validation
- Use `mode: 'onBlur'` with `reValidateMode: 'onChange'`
- Use `noValidate` on `<form>` element
- Define schemas at module level, not inside components
- Derive types with `z.infer<typeof schema>`

## Accessibility Rules
- Every input needs `aria-invalid={!!errors.fieldName}`
- Every input needs `aria-describedby` pointing to its error element
- Error messages need `role="alert"`
- Use `<fieldset>` + `<legend>` for radio/checkbox groups

## State Rules
- Disable submit button with `isSubmitting`
- Show loading indicator during submission
- Use `setError()` for field-level server errors
- Use `useState` for general server errors
- Call `reset()` after successful submission

## Validation Patterns
- Required fields: chain `.min(1, 'Required')` before other validators
- Optional fields: use `.optional().or(z.literal(''))`
- Cross-field validation: use `.refine()` with `path` option

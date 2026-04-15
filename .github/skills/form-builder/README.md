# form-builder

[![Install with APM](https://img.shields.io/badge/📦_Install_with-APM-blue?style=flat-square)](https://github.com/danielmeppiel/apm)

An Agent Skill for building production-ready forms in React/TypeScript projects.

## What This Skill Provides

**Capabilities** for AI agents to build forms:
- React Hook Form patterns and component structure
- Zod validation schemas and custom validators  
- Form state management (loading, errors, multi-step)
- Complete, runnable examples

This skill focuses on *how to build forms*. It complements but does not overlap with:
- [compliance-rules](https://github.com/danielmeppiel/compliance-rules) - GDPR, data handling policies
- [design-guidelines](https://github.com/danielmeppiel/design-guidelines) - Accessibility, visual design

## Install

```bash
apm install danielmeppiel/form-builder
```

## Usage

After installation, your AI agent will automatically apply form-building patterns when you:
- Ask to create a contact form, signup form, etc.
- Request form validation
- Need multi-step form wizards
- Want to handle form submissions

## What's Included

```
form-builder/
├── SKILL.md                         # Main skill file (agentskills.io format)
├── .apm/instructions/
│   ├── react-forms.instructions.md  # React Hook Form patterns
│   ├── validation.instructions.md   # Zod schemas and validators
│   └── state-management.instructions.md  # Loading, errors, multi-step
└── examples/
    ├── contact-form.tsx             # Complete reference implementation
    └── newsletter-signup.tsx        # Minimal example
```

## Core Stack

| Library | Purpose |
|---------|---------|
| [React Hook Form](https://react-hook-form.com/) | Form state, minimal re-renders |
| [Zod](https://zod.dev/) | TypeScript-first validation |
| [@hookform/resolvers](https://github.com/react-hook-form/resolvers) | Zod integration |

## Quick Example

```tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const schema = z.object({
  email: z.string().email(),
  name: z.string().min(2),
});

type FormData = z.infer<typeof schema>;

function MyForm() {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <input {...register('name')} />
      {errors.name && <span role="alert">{errors.name.message}</span>}
      
      <input {...register('email')} />
      {errors.email && <span role="alert">{errors.email.message}</span>}
      
      <button type="submit">Submit</button>
    </form>
  );
}
```

## License

MIT

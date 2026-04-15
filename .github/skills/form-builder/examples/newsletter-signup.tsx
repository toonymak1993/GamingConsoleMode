/**
 * Newsletter Signup - Minimal Form Example
 * 
 * A simple, single-field form demonstrating the essential patterns:
 * - React Hook Form + Zod
 * - Accessibility basics
 * - Loading and success states
 * 
 * Use this as a starting point for simple forms.
 */

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

// Schema: Just an email field
const schema = z.object({
  email: z.string().min(1, 'Email is required').email('Invalid email'),
});

type FormData = z.infer<typeof schema>;

export function NewsletterSignup() {
  const [isSuccess, setIsSuccess] = useState(false);
  
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (data: FormData) => {
    await fetch('/api/newsletter', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    
    setIsSuccess(true);
    reset();
  };

  if (isSuccess) {
    return (
      <div role="status" className="newsletter-success">
        <p>Thanks for subscribing! 🎉</p>
        <button type="button" onClick={() => setIsSuccess(false)}>
          Subscribe another email
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="newsletter-form">
      <div className="newsletter-input-group">
        <label htmlFor="newsletter-email" className="sr-only">
          Email address
        </label>
        <input
          id="newsletter-email"
          type="email"
          placeholder="Enter your email"
          autoComplete="email"
          {...register('email')}
          aria-invalid={!!errors.email}
          aria-describedby={errors.email ? 'newsletter-email-error' : undefined}
        />
        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Subscribing...' : 'Subscribe'}
        </button>
      </div>
      
      {errors.email && (
        <span id="newsletter-email-error" role="alert" className="field-error">
          {errors.email.message}
        </span>
      )}
    </form>
  );
}

export default NewsletterSignup;

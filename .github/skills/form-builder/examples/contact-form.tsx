/**
 * Contact Form - Complete Reference Implementation
 * 
 * This example demonstrates all key patterns from the form-builder skill:
 * - React Hook Form with Zod validation
 * - Proper accessibility (labels, error associations, ARIA)
 * - Loading and error states
 * - Server error handling
 * - Form reset after success
 * 
 * @example
 * ```tsx
 * <ContactForm onSuccess={(data) => console.log('Submitted:', data)} />
 * ```
 */

import { useState } from 'react';
import { useForm, SubmitHandler } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

// =============================================================================
// SCHEMA DEFINITION
// =============================================================================

/**
 * Zod schema for contact form validation.
 * 
 * Key patterns:
 * - .min(1) for required fields (better error message than .nonempty())
 * - .email() for email validation
 * - Optional fields with .optional().or(z.literal(''))
 * - Checkbox consent with .refine()
 */
const contactFormSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .min(2, 'Name must be at least 2 characters'),
  
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  
  phone: z
    .string()
    .regex(/^\+?[\d\s\-()]*$/, 'Please enter a valid phone number')
    .optional()
    .or(z.literal('')),
  
  subject: z.enum(['general', 'support', 'sales', 'partnership'], {
    errorMap: () => ({ message: 'Please select a subject' }),
  }),
  
  message: z
    .string()
    .min(1, 'Message is required')
    .min(10, 'Message must be at least 10 characters')
    .max(1000, 'Message must be less than 1000 characters'),
  
  newsletter: z.boolean().default(false),
  
  consent: z
    .boolean()
    .refine((val) => val === true, {
      message: 'You must agree to our privacy policy to continue',
    }),
});

// Infer TypeScript type from schema - single source of truth
type ContactFormData = z.infer<typeof contactFormSchema>;

// =============================================================================
// COMPONENT PROPS
// =============================================================================

interface ContactFormProps {
  /** Called when form is successfully submitted */
  onSuccess?: (data: ContactFormData) => void;
  /** API endpoint to submit to (default: /api/contact) */
  apiEndpoint?: string;
}

// =============================================================================
// COMPONENT
// =============================================================================

export function ContactForm({ 
  onSuccess, 
  apiEndpoint = '/api/contact' 
}: ContactFormProps) {
  // ---------------------------------------------------------------------------
  // State
  // ---------------------------------------------------------------------------
  
  // Server error state - separate from validation errors
  const [serverError, setServerError] = useState<string | null>(null);
  
  // Success state - show thank you message
  const [isSuccess, setIsSuccess] = useState(false);

  // ---------------------------------------------------------------------------
  // Form Setup
  // ---------------------------------------------------------------------------
  
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
    setError,
  } = useForm<ContactFormData>({
    resolver: zodResolver(contactFormSchema),
    mode: 'onBlur',           // Validate on blur (not on every keystroke)
    reValidateMode: 'onChange', // Re-validate on change after first error
    defaultValues: {
      name: '',
      email: '',
      phone: '',
      subject: undefined,
      message: '',
      newsletter: false,
      consent: false,
    },
  });

  // ---------------------------------------------------------------------------
  // Submit Handler
  // ---------------------------------------------------------------------------
  
  const onSubmit: SubmitHandler<ContactFormData> = async (data) => {
    // Clear any previous server error
    setServerError(null);

    try {
      const response = await fetch(apiEndpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        
        // Handle field-specific errors from server
        if (error.fieldErrors) {
          Object.entries(error.fieldErrors).forEach(([field, message]) => {
            setError(field as keyof ContactFormData, {
              type: 'server',
              message: message as string,
            });
          });
          return;
        }
        
        // Handle general server error
        throw new Error(error.message || 'Failed to send message');
      }

      // Success!
      setIsSuccess(true);
      reset();
      onSuccess?.(data);
      
    } catch (error) {
      setServerError(
        error instanceof Error 
          ? error.message 
          : 'Something went wrong. Please try again.'
      );
    }
  };

  // ---------------------------------------------------------------------------
  // Success View
  // ---------------------------------------------------------------------------
  
  if (isSuccess) {
    return (
      <div 
        className="contact-form-success"
        role="status" 
        aria-live="polite"
      >
        <h2>Thank you!</h2>
        <p>Your message has been sent successfully. We'll get back to you soon.</p>
        <button 
          type="button" 
          onClick={() => setIsSuccess(false)}
          className="btn btn-secondary"
        >
          Send another message
        </button>
      </div>
    );
  }

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------
  
  return (
    <form 
      onSubmit={handleSubmit(onSubmit)} 
      noValidate // Disable browser validation - we use Zod
      className="contact-form"
    >
      {/* Server Error Banner */}
      {serverError && (
        <div role="alert" className="error-banner">
          <span>{serverError}</span>
          <button
            type="button"
            onClick={() => setServerError(null)}
            aria-label="Dismiss error"
            className="error-dismiss"
          >
            ×
          </button>
        </div>
      )}

      {/* Name Field */}
      <div className="form-field">
        <label htmlFor="name">
          Name <span aria-hidden="true">*</span>
        </label>
        <input
          id="name"
          type="text"
          autoComplete="name"
          {...register('name')}
          aria-required="true"
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? 'name-error' : undefined}
        />
        {errors.name && (
          <span id="name-error" role="alert" className="field-error">
            {errors.name.message}
          </span>
        )}
      </div>

      {/* Email Field */}
      <div className="form-field">
        <label htmlFor="email">
          Email <span aria-hidden="true">*</span>
        </label>
        <input
          id="email"
          type="email"
          autoComplete="email"
          {...register('email')}
          aria-required="true"
          aria-invalid={!!errors.email}
          aria-describedby={errors.email ? 'email-error' : undefined}
        />
        {errors.email && (
          <span id="email-error" role="alert" className="field-error">
            {errors.email.message}
          </span>
        )}
      </div>

      {/* Phone Field (Optional) */}
      <div className="form-field">
        <label htmlFor="phone">Phone (optional)</label>
        <input
          id="phone"
          type="tel"
          autoComplete="tel"
          {...register('phone')}
          aria-invalid={!!errors.phone}
          aria-describedby={errors.phone ? 'phone-error' : undefined}
        />
        {errors.phone && (
          <span id="phone-error" role="alert" className="field-error">
            {errors.phone.message}
          </span>
        )}
      </div>

      {/* Subject Select */}
      <div className="form-field">
        <label htmlFor="subject">
          Subject <span aria-hidden="true">*</span>
        </label>
        <select
          id="subject"
          {...register('subject')}
          aria-required="true"
          aria-invalid={!!errors.subject}
          aria-describedby={errors.subject ? 'subject-error' : undefined}
        >
          <option value="">Select a subject...</option>
          <option value="general">General Inquiry</option>
          <option value="support">Technical Support</option>
          <option value="sales">Sales Question</option>
          <option value="partnership">Partnership Opportunity</option>
        </select>
        {errors.subject && (
          <span id="subject-error" role="alert" className="field-error">
            {errors.subject.message}
          </span>
        )}
      </div>

      {/* Message Textarea */}
      <div className="form-field">
        <label htmlFor="message">
          Message <span aria-hidden="true">*</span>
        </label>
        <textarea
          id="message"
          rows={5}
          {...register('message')}
          aria-required="true"
          aria-invalid={!!errors.message}
          aria-describedby={errors.message ? 'message-error' : 'message-hint'}
        />
        <span id="message-hint" className="field-hint">
          10-1000 characters
        </span>
        {errors.message && (
          <span id="message-error" role="alert" className="field-error">
            {errors.message.message}
          </span>
        )}
      </div>

      {/* Newsletter Checkbox (Optional) */}
      <div className="form-field form-field-checkbox">
        <label>
          <input
            type="checkbox"
            {...register('newsletter')}
          />
          <span>Subscribe to our newsletter for updates</span>
        </label>
      </div>

      {/* Consent Checkbox (Required) */}
      <div className="form-field form-field-checkbox">
        <label>
          <input
            type="checkbox"
            {...register('consent')}
            aria-required="true"
            aria-invalid={!!errors.consent}
            aria-describedby={errors.consent ? 'consent-error' : undefined}
          />
          <span>
            I agree to the <a href="/privacy" target="_blank" rel="noopener">privacy policy</a> <span aria-hidden="true">*</span>
          </span>
        </label>
        {errors.consent && (
          <span id="consent-error" role="alert" className="field-error">
            {errors.consent.message}
          </span>
        )}
      </div>

      {/* Submit Button */}
      <button 
        type="submit" 
        disabled={isSubmitting}
        className="btn btn-primary"
        aria-busy={isSubmitting}
      >
        {isSubmitting ? (
          <>
            <span className="spinner" aria-hidden="true" />
            Sending...
          </>
        ) : (
          'Send Message'
        )}
      </button>
    </form>
  );
}

export default ContactForm;

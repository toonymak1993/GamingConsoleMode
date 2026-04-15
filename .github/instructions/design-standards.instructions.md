---
applyTo: "**/*.{jsx,tsx,vue,html,css,scss,sass,less}"
description: "UI/UX design standards and accessibility guidelines"
---

# Design System & Accessibility Standards

## Color & Contrast Standards

### Corporate Brand Colors
- **Primary Accent**: Dark green (`#059669` or similar dark green shades)
- **Background**: White (`#ffffff`)
- **Note**: All corporate branding must use dark green accents with white backgrounds

### Color Palette
- **Primary**: Use high-contrast colors that meet WCAG AA standards (4.5:1 minimum)
- **Secondary**: Ensure color combinations work for colorblind users
- **Status Colors**: Use consistent semantic colors across the application
  - Success: #28a745 (green)
  - Warning: #ffc107 (amber)
  - Error: #dc3545 (red)
  - Info: #17a2b8 (blue)

### Accessibility Requirements
- All interactive elements must have focus indicators
- Color should never be the only means of conveying information
- Text must maintain minimum contrast ratios
- Images must include appropriate alt text

## Typography Standards

### Font Hierarchy
- **Headings**: Use semantic HTML headings (h1-h6) in logical order
- **Body Text**: Minimum 16px font size for accessibility
- **Line Height**: 1.5x font size minimum for readability
- **Font Weights**: Use consistent weight scale (400, 600, 700)

### Responsive Typography
```css
/* Example responsive font scale */
h1 { font-size: clamp(1.75rem, 4vw, 3rem); }
h2 { font-size: clamp(1.5rem, 3.5vw, 2.5rem); }
body { font-size: clamp(1rem, 2.5vw, 1.125rem); }
```

## Layout & Spacing

### Grid System
- Use CSS Grid or Flexbox for layouts
- Maintain consistent spacing scale (8px base unit)
- Ensure responsive breakpoints at: 480px, 768px, 1024px, 1440px

### Component Standards
- **Buttons**: Minimum 44px touch target size
- **Form Fields**: Clear labels and error states
- **Navigation**: Keyboard accessible with proper ARIA labels
- **Cards**: Consistent padding and border radius

## Mobile-First Design

### Touch Targets
- Minimum 44x44px for touch elements
- Adequate spacing between interactive elements
- Consider thumb reach zones in mobile layouts

### Performance
- Optimize images with appropriate formats (WebP, AVIF)
- Use lazy loading for below-the-fold content
- Minimize layout shifts (CLS)

## Component Checklist

### Before Implementation
- [ ] Meets WCAG 2.1 AA accessibility standards
- [ ] Works with keyboard navigation only
- [ ] Compatible with screen readers
- [ ] Tested across major browsers
- [ ] Responsive across all breakpoints
- [ ] Consistent with design system tokens

### Testing Requirements
- [ ] Color contrast verification
- [ ] Screen reader testing
- [ ] Keyboard navigation testing
- [ ] Mobile device testing
- [ ] Cross-browser compatibility check

**Remember**: Design impacts all users. Always consider accessibility and inclusive design principles from the start of development.
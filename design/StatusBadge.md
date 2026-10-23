# StatusBadge

Shows an employee, request or document state as a colored dot plus a word; color never carries the meaning alone.

The consumer provides the label and the tone. Tones: `hr-badge--ok` (active, approved, complete), `hr-badge--warn` (on leave, pending, expiring), `hr-badge--danger` (suspended, rejected, expired), no modifier for neutral states (retired, draft), and `hr-badge--brand hr-badge--plain` for non-status tags like grade or contract type.

The badge sits on `surface-2` with the status color on the dot and text, which holds at least 4.5:1 in both themes. Keep labels to one or two words. Don't use a badge as a button.

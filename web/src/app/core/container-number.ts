// ISO 6346 container number check.
//
// This DUPLICATES the backend rule (src/DepotFlow.Domain/ContainerNumber.cs) on purpose: it only exists to give
// instant feedback while typing. The server stays the authority and re-validates every request.

const PATTERN = /^[A-Z]{3}[UJZ][0-9]{6}[0-9]$/;

/** Value of a character in the check digit sum: digits are themselves; letters start at A = 10 and skip multiples of 11. */
function characterValue(c: string): number {
  if (c >= '0' && c <= '9') {
    return c.charCodeAt(0) - 48;
  }

  let value = 10 + (c.charCodeAt(0) - 65);
  if (value > 10) value++;   // after A
  if (value > 21) value++;   // after K
  if (value > 32) value++;   // after U
  return value;
}

/** The ISO 6346 check digit of the first 10 characters: sum of value x 2^position, modulo 11, with 10 read as 0. */
export function computeCheckDigit(first10Characters: string): number {
  let sum = 0;
  for (let i = 0; i < 10; i++) {
    sum += characterValue(first10Characters[i]) * 2 ** i;
  }
  return (sum % 11) % 10;
}

export interface ContainerNumberCheck {
  valid: boolean;
  /** The normalised number (trimmed, upper case) when valid. */
  value?: string;
  error?: string;
}

export function checkContainerNumber(input: string | null | undefined): ContainerNumberCheck {
  const normalised = (input ?? '').trim().toUpperCase();

  if (normalised === '') {
    return { valid: false, error: 'Container number is required.' };
  }

  if (!PATTERN.test(normalised)) {
    return { valid: false, error: 'Use 3 letters, U/J/Z, 6 digits and a check digit (for example CSQU3054383).' };
  }

  const expected = computeCheckDigit(normalised.slice(0, 10));
  if (Number(normalised[10]) !== expected) {
    return { valid: false, error: `Check digit is wrong; expected ${expected}.` };
  }

  return { valid: true, value: normalised };
}

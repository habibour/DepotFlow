import { checkContainerNumber, computeCheckDigit } from './container-number';

// Mirrors the backend's ContainerNumberTests (spec S1-05), so the two implementations agree.
describe('container number check', () => {
  it.each(['CSQU3054383', 'MSCU1234566', 'MAEU1234567', 'TEMU6543211', 'HLXU2000001', 'MSKU0000006', 'CMAU7654327', 'ONEU9876541'])(
    'accepts the known valid number %s',
    (number) => {
      expect(checkContainerNumber(number)).toEqual({ valid: true, value: number });
    },
  );

  it('normalises case and surrounding spaces', () => {
    expect(checkContainerNumber('  csqu3054383 ')).toEqual({ valid: true, value: 'CSQU3054383' });
  });

  it('rejects a wrong check digit and says which digit was expected', () => {
    const result = checkContainerNumber('CSQU3054384');
    expect(result.valid).toBe(false);
    expect(result.error).toContain('expected 3');
  });

  it.each(['CSQU305438', 'CSQU30543831', 'CSQ13054383', 'CSQU-3054383', 'CSQX3054383'])('rejects the malformed number %s', (number) => {
    expect(checkContainerNumber(number).valid).toBe(false);
  });

  it.each(['', '   ', null, undefined])('rejects empty input (%s)', (input) => {
    const result = checkContainerNumber(input);
    expect(result.valid).toBe(false);
    expect(result.error).toBe('Container number is required.');
  });

  it('computes the check digit of the first ten characters', () => {
    expect(computeCheckDigit('CSQU305438')).toBe(3);
    expect(computeCheckDigit('MSKU000000')).toBe(6);
  });

  it('accepts the J and Z category letters as well as U', () => {
    const first10 = 'ABCJ123456';
    expect(checkContainerNumber(first10 + computeCheckDigit(first10)).valid).toBe(true);
    const z = 'ABCZ123456';
    expect(checkContainerNumber(z + computeCheckDigit(z)).valid).toBe(true);
  });
});

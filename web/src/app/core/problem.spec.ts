import { HttpErrorResponse } from '@angular/common/http';
import { describeError } from './problem';

const http = (status: number, body: unknown) => new HttpErrorResponse({ status, error: body });

describe('describeError', () => {
  it('shows the problem title for a business error', () => {
    const result = describeError(http(409, { title: 'This container already has an active visit.', code: 'container_already_in_yard', traceId: 't-1' }));
    expect(result.message).toBe('This container already has an active visit.');
    expect(result.fields).toEqual([]);
    expect(result.traceId).toBe('t-1');
  });

  it('prefers the detail over the title', () => {
    expect(describeError(http(401, { title: 'Invalid credentials', detail: 'Email or password is incorrect.' })).message).toBe('Email or password is incorrect.');
  });

  it('lists the field errors of a validation problem', () => {
    const result = describeError(http(400, { title: 'One or more validation errors occurred.', errors: { sealNumber: ['must not be empty'], sizeFeet: ['must be 20 or 40'] } }));
    expect(result.fields).toEqual(['sealNumber: must not be empty', 'sizeFeet: must be 20 or 40']);
  });

  it('explains a network failure', () => {
    expect(describeError(http(0, null)).message).toContain('Cannot reach the server');
  });

  it('falls back to the status when the body is not a problem', () => {
    expect(describeError(http(502, '<html>Bad gateway</html>')).message).toBe('The request failed (HTTP 502).');
  });

  it('handles plain errors and unknown values', () => {
    expect(describeError(new Error('boom')).message).toBe('boom');
    expect(describeError('what').message).toBe('Something went wrong.');
  });
});

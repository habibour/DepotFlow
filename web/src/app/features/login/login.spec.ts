import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth.service';
import { Login } from './login';

async function setup(login: () => Promise<void>) {
  const auth = { login: vi.fn(login), homePath: () => '/gate' };
  TestBed.configureTestingModule({ imports: [Login], providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
  const router = TestBed.inject(Router);
  const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  const fixture = TestBed.createComponent(Login);
  await fixture.whenStable();
  return { fixture, auth, navigate, el: fixture.nativeElement as HTMLElement };
}

function type(el: HTMLElement, selector: string, value: string): void {
  const input = el.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

const submit = (el: HTMLElement) => el.querySelector('form')!.dispatchEvent(new Event('submit'));

describe('Login', () => {
  it('signs in and goes to the role home', async () => {
    const { fixture, auth, navigate, el } = await setup(() => Promise.resolve());
    type(el, '#email', 'gate@depotflow.local');
    type(el, '#password', 'secret');

    submit(el);
    await fixture.whenStable();

    expect(auth.login).toHaveBeenCalledWith('gate@depotflow.local', 'secret');
    expect(navigate).toHaveBeenCalledWith('/gate');
  });

  it('shows the server message when the credentials are wrong and does not navigate', async () => {
    const error = new HttpErrorResponse({ status: 401, error: { title: 'Invalid credentials', detail: 'Email or password is incorrect.' } });
    const { fixture, navigate, el } = await setup(() => Promise.reject(error));
    type(el, '#email', 'gate@depotflow.local');
    type(el, '#password', 'nope');

    submit(el);
    await fixture.whenStable();

    expect(el.querySelector('.alert.error')?.textContent).toContain('Email or password is incorrect.');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not call the API for an empty or invalid form and explains what is missing', async () => {
    const { fixture, auth, el } = await setup(() => Promise.resolve());
    type(el, '#email', 'not-an-email');

    submit(el);
    await fixture.whenStable();

    expect(auth.login).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Enter a valid email address.');
    expect(el.textContent).toContain('Enter your password.');
  });

  it('fills the form from a demo account button', async () => {
    const { fixture, el } = await setup(() => Promise.resolve());

    [...el.querySelectorAll('.chips button')].find((b) => b.textContent === 'Billing officer')!.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(el.querySelector<HTMLInputElement>('#email')!.value).toBe('billing@depotflow.local');
    expect(el.querySelector<HTMLInputElement>('#password')!.value).not.toBe('');
  });
});

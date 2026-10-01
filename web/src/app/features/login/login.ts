import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { describeError } from '../../core/problem';

// The four users the API seeds for demos, with the demo-only password published in the README.
const DEMO_PASSWORD = 'Demo#DepotFlow1';
const DEMO_ACCOUNTS = [
  { email: 'gate@depotflow.local', label: 'Gate clerk' },
  { email: 'billing@depotflow.local', label: 'Billing officer' },
  { email: 'yard@depotflow.local', label: 'Yard planner' },
  { email: 'admin@depotflow.local', label: 'Admin' },
];

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly demoAccounts = DEMO_ACCOUNTS;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected useDemo(email: string): void {
    this.form.setValue({ email, password: DEMO_PASSWORD });
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.login(email, password);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(returnUrl && returnUrl !== '/login' ? returnUrl : this.auth.homePath());
    } catch (error) {
      this.error.set(describeError(error).message);
    } finally {
      this.busy.set(false);
    }
  }
}

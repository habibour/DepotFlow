import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Adds the bearer token to API calls, and sends the user to the login screen when the API says the token is no good. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isApiCall = request.url.startsWith('/api/');
  const isLogin = request.url.endsWith('/auth/login');
  const token = auth.token;

  const outgoing = isApiCall && !isLogin && token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      // A 401 from the login call itself just means wrong credentials: let the login screen show it.
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiCall && !isLogin) {
        auth.logout();
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    }),
  );
};

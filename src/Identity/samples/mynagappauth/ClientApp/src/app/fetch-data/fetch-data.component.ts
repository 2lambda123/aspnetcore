import { Component, Inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { NgForm } from '@angular/forms';

declare let window: any;

@Component({
  selector: 'app-fetch-data',
  templateUrl: './fetch-data.component.html'
})
export class FetchDataComponent {
  public fx?: Effects;
  public authenticated: boolean = false;
  public register: boolean = false;
  public operationFailed: boolean = false;
  public operationReason: string = "";
  public accessToken?: string;

  public fetch() {
    this.operationFailed = false;

    let headers = this.accessToken ? { "Authorization": "Bearer " + this.accessToken } : new HttpHeaders();

    this.http.get<Effects>(this.baseUrl + 'effects', { headers }).subscribe(result => {
      this.fx = result;
      if (this.fx.username) {
        this.authenticated = true;
        window.canvasStart();
      }
      else {
        this.authenticated = false;
      }
    }, _ => {
      this.authenticated = false;
      this.operationFailed = true;
      this.operationReason = "Request for effects failed.";
    });
  }

  public toggleRegister(f: NgForm) {
    f.resetForm();
    this.register = this.register ? false : true;
  };

  public validate(f: NgForm) {
    this.operationFailed = false;
    if (f.valid) {
      if (this.register) {
        if (f.value.password === f.value.pconfirm) {
          this.submitRegistration(f.value);
        }
        else {
          this.operationFailed = true;
          this.operationReason = "Passwords do not match";
        }
      }
      else {
        this.login(f.value);
      }
    }
    else {
      this.operationFailed = true;
      this.operationReason = "Form validation failed.";
    }
  }

  public submitRegistration(upwd: UserPwd) {
    this.operationFailed = false;
    this.http.post(this.baseUrl + 'identity/v1/register', {
      username: upwd.username, password: upwd.password
    }).subscribe(_ => {
      this.register = false;
      alert('You successfully registered. Now login!');
    }, error => {
      this.operationFailed = true;
      this.operationReason = "Registration failed.";
    });
  };

  public login(uwpd: UserPwd) {
    let cookieMode = !!uwpd.cookieMode;
    this.operationFailed = false;

    this.http.post<LoginResponse>(this.baseUrl + 'identity/v1/login', {
      username: uwpd.username, password: uwpd.password, cookieMode: cookieMode
    }).subscribe(response => {
      this.authenticated = true;
      if (!cookieMode) {
        this.accessToken = response.access_token;
      }
      this.fetch();
    }, _ => {
      this.authenticated = false;
      this.operationFailed = true;
      this.operationReason = "Login failed. Try again!";
    });
  };

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) {
    this.fetch();
  }
}

interface Effects {
  username: string;
  effects: string[];
}

interface UserPwd {
  username: string;
  password: string;
  cookieMode: boolean;
}

interface LoginResponse {
  token_type: string;
  access_token: string;
  expires_in: number;
}

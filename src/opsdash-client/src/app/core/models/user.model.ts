export interface User {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  roleId: number;
  isActive: boolean;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  tokenExpiration: string;
  userId: number;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  tenantId: number;
  tenantName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  subdomain: string;
}

export interface RegisterRequest {
  tenantName: string;
  subdomain: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

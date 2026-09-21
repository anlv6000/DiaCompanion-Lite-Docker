import type { Role } from "@/types/api";
import { hasAnyRole, hasRole, primaryWebRole, type RoleSource } from "@/lib/roles";

/**
 * Quyền hiển thị phía client. Backend vẫn là lớp kiểm soát quyền cuối cùng.
 * Mọi hàm nhận RoleSource để hỗ trợ một User có nhiều role.
 */
export const can = {
  reviewDiagnosis: (r: RoleSource) => hasRole(r, "Doctor"),

  voidPatient: (r: RoleSource) => hasRole(r, "Receptionist"),
  voidVisit: (r: RoleSource) => hasAnyRole(r, ["Doctor", "Receptionist"]),
  voidImage: (r: RoleSource) => hasRole(r, "Doctor"),
  voidDiagnosis: (r: RoleSource) => hasRole(r, "Doctor"),
  voidPrescription: (r: RoleSource) => hasRole(r, "Doctor"),
  voidReview: (r: RoleSource) => hasRole(r, "Doctor"),

  closeVisit: (r: RoleSource) => hasRole(r, "Doctor"),
  prescribe: (r: RoleSource) => hasRole(r, "Doctor"),
  manageImages: (r: RoleSource) => hasRole(r, "Doctor"),

  reissuePatientCredential: (r: RoleSource) => hasRole(r, "Receptionist"),
  createPatient: (r: RoleSource) => hasRole(r, "Receptionist"),
  createVisit: (r: RoleSource) => hasRole(r, "Receptionist"),
  manageShifts: (r: RoleSource) => hasRole(r, "Receptionist"),
  viewRecheck: (r: RoleSource) => hasAnyRole(r, ["Doctor", "Receptionist"]),
};

export function homeRouteFor(source?: RoleSource): string {
  const role = primaryWebRole(source);
  switch (role) {
    case "Doctor": return "/triage";
    case "Admin": return "/dashboard";
    case "Receptionist": return "/reception/visits/new";
    default: return "/login";
  }
}

export function resolveLandingRoute(source?: RoleSource, defaultRoute?: string): string {
  if (defaultRoute && defaultRoute !== "/home") return defaultRoute;
  return homeRouteFor(source);
}

export type { Role };

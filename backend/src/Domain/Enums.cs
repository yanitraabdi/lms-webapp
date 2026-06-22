namespace Academy.Domain.Enums;

public enum UserRole { User, Admin, SuperAdmin }
public enum UserStatus { Active, Suspended, Deleted }

public enum SeatStatus { Unassigned, Assigned, Revoked }
public enum OrgMemberRole { Member, OrgAdmin, BillingOwner }

public enum BillingCycle { Monthly, Annual }
public enum SubscriptionStatus { Active, PastDue, Grace, Canceled, Expired }

public enum ModuleStatus { Draft, Published }
public enum ResourceType { Pdf, Link }

public enum PaymentKind { Cycle, ProrationUpgrade }
public enum PaymentStatus { Pending, Paid, Failed }

public enum NotificationChannel { InApp, Email }
public enum NoteType { Note, Bookmark }
public enum TourStatus { Completed, Skipped }
public enum CapstoneSubmissionStatus { Submitted, Reviewed, Rejected }

public enum UserTokenPurpose { EmailVerification, PasswordReset }

namespace Academy.Domain.Enums;

public enum UserRole { User, Admin, SuperAdmin }
public enum UserStatus { Active, Suspended, Deleted }

public enum SeatStatus { Unassigned, Assigned, Revoked }
public enum OrgMemberRole { Member, OrgAdmin, BillingOwner }

public enum BillingCycle { Monthly, Annual }
public enum SubscriptionStatus { Active, PastDue, Grace, Canceled, Expired }

public enum ModuleStatus { Draft, Published }
public enum ResourceType { Pdf, Link }

public enum PaymentKind { Cycle, ProrationUpgrade, ProgramPurchase }
public enum PaymentStatus { Pending, Paid, Failed }

// ---- INVERTA: programs & enrollment (FSD §3–§5) ----
public enum ProgramStatus { Draft, Published, Archived }
public enum SessionType { Video, Live, FinalAssessment }
public enum LiveMode { Zoom, Offline }
public enum BatchStatus { Upcoming, Running, Finished }
public enum EnrollmentStatus { PendingPayment, Active, Completed, Revoked }
public enum CompletionMethod { WatchAndTest, Attended, Submitted }

// ---- INVERTA: assessment engine (FSD §6–§7) ----
public enum AssessmentKind { Gating, Final }
public enum QuestionSection { Listening, Reading, Vocabulary, Structure, General }
public enum QuestionType { Mcq }
// Ignored = reported but below the grace threshold: retained for the dispute trail, never a strike.
public enum ProctorEventKind { VisibilityHidden, WindowBlur, FullscreenExit, Warned, AutoSubmitted, Ignored }

public enum NotificationChannel { InApp, Email }
public enum NoteType { Note, Bookmark }
public enum TourStatus { Completed, Skipped }
public enum CapstoneSubmissionStatus { Submitted, Reviewed, Rejected }

public enum UserTokenPurpose { EmailVerification, PasswordReset }

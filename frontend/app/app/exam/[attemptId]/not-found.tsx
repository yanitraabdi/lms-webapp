import Link from "next/link";
import { ErrorState } from "@/components/ui";

export default function ExamNotFound() {
  return (
    <div className="mx-auto max-w-md px-6 py-20">
      <ErrorState
        title="Tes tidak ditemukan"
        message="Tautan tes ini tidak berlaku untuk akun Anda."
        action={<Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>}
      />
    </div>
  );
}

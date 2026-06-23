import { NextResponse } from "next/server";
import { revalidatePath } from "next/cache";

// On-demand ISR revalidation, called by the .NET API after admin content/pricing
// changes (TSD §4.2). Secret-gated; not a public endpoint.
export async function POST(request: Request) {
  let body: { secret?: string; paths?: string[] };
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "bad request" }, { status: 400 });
  }

  const secret = process.env.REVALIDATE_SECRET;
  if (!secret || body.secret !== secret) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }

  const paths = Array.isArray(body.paths) ? body.paths : [];
  for (const p of paths) {
    try {
      revalidatePath(p);
    } catch {
      /* unknown path — ignore */
    }
  }
  return NextResponse.json({ revalidated: true, paths });
}

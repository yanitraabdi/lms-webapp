import { Chapter } from "@/components/ui";
import { ITP_FORMAT, ITP_TOTAL_MINUTES, ITP_TOTAL_QUESTIONS } from "@/lib/itpFormat";

const SECTIONS = [ITP_FORMAT.listening, ITP_FORMAT.structure, ITP_FORMAT.reading];

export function ItpFormat() {
  return (
    <Chapter title="Format tes akhir" intro={`Mengikuti susunan TOEFL ITP. Rentang skor total ${ITP_FORMAT.totalMin}–${ITP_FORMAT.totalMax}.`}>
      <div className="mt-4 overflow-x-auto">
        <table className="data-table">
          <caption className="sr-only">Format tes akhir</caption>
          <thead>
            <tr>
              <th scope="col">Bagian</th>
              <th scope="col" className="num">Soal</th>
              <th scope="col" className="num">Waktu</th>
            </tr>
          </thead>
          <tbody>
            {SECTIONS.map((s) => (
              <tr key={s.label}>
                <td>{s.label}</td>
                <td className="num">{s.questions}</td>
                <td className="num">{s.minutes} menit</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th scope="row">Total</th>
              <td className="num">{ITP_TOTAL_QUESTIONS}</td>
              <td className="num">{ITP_TOTAL_MINUTES} menit</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </Chapter>
  );
}

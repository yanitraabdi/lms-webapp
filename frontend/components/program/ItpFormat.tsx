import { Chapter } from "@/components/ui";
import { ITP_FORMAT, ITP_TOTAL_MINUTES, ITP_TOTAL_QUESTIONS } from "@/lib/itpFormat";

const SECTIONS = [ITP_FORMAT.listening, ITP_FORMAT.structure, ITP_FORMAT.reading];

export function ItpFormat() {
  return (
    <Chapter title="Format tes akhir" intro="Tes akhir program ini mengikuti format TOEFL ITP.">
      <div className="mt-4 overflow-x-auto">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Bagian</th>
              <th scope="col" className="num">Soal</th>
              <th scope="col" className="num">Waktu</th>
              <th scope="col" className="num">Skor skala</th>
            </tr>
          </thead>
          <tbody>
            {SECTIONS.map((s) => (
              <tr key={s.label}>
                <td>{s.label}</td>
                <td className="num">{s.questions}</td>
                <td className="num">{s.minutes} menit</td>
                <td className="num">-</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td>Total</td>
              <td className="num">{ITP_TOTAL_QUESTIONS}</td>
              <td className="num">{ITP_TOTAL_MINUTES} menit</td>
              <td className="num">{ITP_FORMAT.totalMin}–{ITP_FORMAT.totalMax}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </Chapter>
  );
}

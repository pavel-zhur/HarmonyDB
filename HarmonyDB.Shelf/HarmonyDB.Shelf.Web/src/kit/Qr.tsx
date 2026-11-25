import { useMemo } from 'react';
import qrcode from 'qrcode-generator';

/** A QR code of a link, dark on white (it must scan in the dark theme too). */
export function Qr({ text, size = 132 }: { text: string; size?: number }) {
    const svg = useMemo(() => {
        const code = qrcode(0, 'M');
        code.addData(text);
        code.make();
        return code.createSvgTag({ cellSize: 4, margin: 2, scalable: true });
    }, [text]);
    return <span className="qrcode" style={{ width: size, height: size }} role="img" aria-label={text} dangerouslySetInnerHTML={{ __html: svg }} />;
}

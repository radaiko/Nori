import { useEffect, useRef, useState } from 'react';
import { NoriRenderer } from '@nori/renderer';

export default function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<NoriRenderer | null>(null);
  const [connected, setConnected] = useState(false);
  const [fps, setFps] = useState(0);

  useEffect(() => {
    if (!canvasRef.current) return;

    const canvas = canvasRef.current;
    // Match canvas size to window
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;

    const renderer = new NoriRenderer({
      canvas,
      serverUrl: 'ws://localhost:5100/',
    });

    renderer.onConnected = () => setConnected(true);
    renderer.onDisconnected = () => setConnected(false);
    renderer.onFps = setFps;

    rendererRef.current = renderer;
    renderer.connect();

    const handleResize = () => {
      canvas.width = window.innerWidth;
      canvas.height = window.innerHeight;
    };
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      renderer.dispose();
    };
  }, []);

  return (
    <>
      <canvas ref={canvasRef} style={{ display: 'block', width: '100%', height: '100%' }} />
      <div style={{
        position: 'absolute', top: 8, left: 8,
        background: 'rgba(0,0,0,0.6)', color: 'white',
        padding: '4px 8px', borderRadius: 4, fontSize: 12,
        fontFamily: 'monospace',
      }}>
        {connected ? `Connected | ${fps} FPS` : 'Connecting...'}
      </div>
    </>
  );
}

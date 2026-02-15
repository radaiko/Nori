import { useEffect, useRef, useState, useCallback } from 'react';
import { NoriRenderer } from '@nori/renderer';

interface EntityInfo {
  id: number;
  position: { x: number; y: number; z: number };
}

export default function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<NoriRenderer | null>(null);
  const [connected, setConnected] = useState(false);
  const [fps, setFps] = useState(0);
  const [pickedEntity, setPickedEntity] = useState<EntityInfo | null>(null);
  const [serverUrl, setServerUrl] = useState('ws://localhost:5100/');

  const initRenderer = useCallback((url: string) => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    // Dispose previous renderer if any
    if (rendererRef.current) {
      rendererRef.current.dispose();
      rendererRef.current = null;
      setConnected(false);
      setFps(0);
      setPickedEntity(null);
    }

    const updateSize = () => {
      canvas.width = canvas.clientWidth * devicePixelRatio;
      canvas.height = canvas.clientHeight * devicePixelRatio;
    };
    updateSize();

    const renderer = new NoriRenderer({
      canvas,
      serverUrl: url,
    });

    renderer.onConnected = () => setConnected(true);
    renderer.onDisconnected = () => setConnected(false);
    renderer.onFps = setFps;
    renderer.onEntityPicked = (entityId, position) => {
      if (entityId >= 0) {
        setPickedEntity({ id: entityId, position });
      } else {
        setPickedEntity(null);
      }
    };

    rendererRef.current = renderer;
    renderer.connect();
  }, []);

  useEffect(() => {
    initRenderer(serverUrl);

    const handleResize = () => {
      const canvas = canvasRef.current;
      if (canvas) {
        canvas.width = canvas.clientWidth * devicePixelRatio;
        canvas.height = canvas.clientHeight * devicePixelRatio;
      }
    };
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      rendererRef.current?.dispose();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleConnect = useCallback(() => {
    initRenderer(serverUrl);
  }, [serverUrl, initRenderer]);

  const handleResetView = useCallback(() => {
    rendererRef.current?.resetView();
  }, []);

  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
      {/* Toolbar */}
      <div style={{
        height: 40, display: 'flex', alignItems: 'center', gap: 8,
        padding: '0 12px',
        background: '#1a1a2e', borderBottom: '1px solid #333',
        color: '#ccc', fontFamily: 'system-ui, sans-serif', fontSize: 13,
      }}>
        <span style={{ fontWeight: 600, color: '#fff', marginRight: 12 }}>Nori</span>

        {/* Connection indicator */}
        <span style={{
          width: 8, height: 8, borderRadius: '50%',
          background: connected ? '#4caf50' : '#f44336',
          display: 'inline-block',
        }} />
        <span>{connected ? 'Connected' : 'Disconnected'}</span>

        <div style={{ flex: 1 }} />

        {/* Server URL input */}
        <input
          type="text"
          value={serverUrl}
          onChange={(e) => setServerUrl(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') handleConnect(); }}
          style={{
            padding: '4px 8px', border: '1px solid #555', borderRadius: 4,
            background: '#2a2a3e', color: '#ccc', fontSize: 12,
            width: 250, fontFamily: 'monospace',
          }}
        />
        <button onClick={handleConnect} style={btnStyle}>Connect</button>

        {/* Viewport controls */}
        <button onClick={handleResetView} style={btnStyle}>
          Zoom Extents
        </button>

        {/* FPS */}
        <span style={{ fontFamily: 'monospace', color: '#888' }}>
          {fps} FPS
        </span>
      </div>

      {/* Main viewport */}
      <div style={{ flex: 1, position: 'relative' }}>
        <canvas ref={canvasRef} style={{ display: 'block', width: '100%', height: '100%' }} />

        {/* Picked entity info */}
        {pickedEntity && (
          <div style={{
            position: 'absolute', bottom: 12, left: 12,
            background: 'rgba(0,0,0,0.75)', color: '#fff',
            padding: '8px 12px', borderRadius: 6, fontSize: 12,
            fontFamily: 'monospace',
          }}>
            Entity #{pickedEntity.id} at ({
              pickedEntity.position.x.toFixed(2)}, {
              pickedEntity.position.y.toFixed(2)}, {
              pickedEntity.position.z.toFixed(2)})
          </div>
        )}
      </div>
    </div>
  );
}

const btnStyle: React.CSSProperties = {
  padding: '4px 12px', border: '1px solid #555', borderRadius: 4,
  background: '#2a2a3e', color: '#ccc', cursor: 'pointer',
  fontSize: 12,
};

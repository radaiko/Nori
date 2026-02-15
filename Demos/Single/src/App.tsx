import { useEffect, useRef, useState, useCallback } from 'react';
import { NoriRenderer } from '@nori/renderer';

const DEMOS = [
  { id: 'leaf', label: 'Polygon Fill' },
  { id: 'linefont', label: 'Line Fonts' },
  { id: 'mesh', label: 'Load TMesh' },
  { id: 'tess', label: 'Tessellation' },
  { id: 'boolean', label: 'Poly Boolean' },
  { id: 'dwg', label: 'Load DXF' },
  { id: 'robot', label: 'Robot IK/FK' },
  { id: 'stp', label: 'Load STEP' },
  { id: 'aabbtree', label: 'AABB Tree' },
  { id: 'mes', label: 'Min. Sphere' },
  { id: 't3x', label: 'Load T3X File' },
  { id: 'meshslice', label: 'Slice Mesh' },
  { id: 'convexhull', label: 'Convex Hull' },
  { id: 'obb', label: 'Build OBB' },
];

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
  const [selectedDemo, setSelectedDemo] = useState('leaf');

  useEffect(() => {
    if (!canvasRef.current) return;

    const canvas = canvasRef.current;
    const updateSize = () => {
      canvas.width = canvas.clientWidth * devicePixelRatio;
      canvas.height = canvas.clientHeight * devicePixelRatio;
    };
    updateSize();

    const renderer = new NoriRenderer({
      canvas,
      serverUrl: 'ws://localhost:5100/',
    });

    renderer.onConnected = () => {
      setConnected(true);
      renderer.sendCommand('demo', 'leaf');
    };
    renderer.onDisconnected = () => setConnected(false);
    renderer.onFps = setFps;
    renderer.onSceneLoaded = () => renderer.resetView();
    renderer.onEntityPicked = (entityId, position) => {
      if (entityId >= 0) {
        setPickedEntity({ id: entityId, position });
      } else {
        setPickedEntity(null);
      }
    };

    rendererRef.current = renderer;
    renderer.connect().catch(err => console.error('Failed to connect:', err));

    const handleResize = () => {
      updateSize();
    };
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      renderer.dispose();
    };
  }, []);

  const handleDemoClick = useCallback((demoId: string) => {
    setSelectedDemo(demoId);
    rendererRef.current?.sendCommand('demo', demoId);
  }, []);

  const handleResetView = useCallback(() => {
    rendererRef.current?.resetView();
  }, []);

  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'row' }}>
      {/* Left sidebar */}
      <div style={{
        width: 130, display: 'flex', flexDirection: 'column',
        background: '#888', padding: '4px 0',
        fontFamily: 'system-ui, sans-serif', fontSize: 12,
        flexShrink: 0,
      }}>
        {/* Title */}
        <div style={{
          padding: '4px 8px 8px', fontWeight: 700, fontSize: 13,
          color: '#000', borderBottom: '1px solid #777', marginBottom: 4,
          display: 'flex', alignItems: 'center', gap: 6,
        }}>
          NORI Demos
          <span style={{
            width: 8, height: 8, borderRadius: '50%', display: 'inline-block',
            background: connected ? '#4caf50' : '#f44336',
          }} />
        </div>

        {/* Demo buttons */}
        {DEMOS.map(d => (
          <button
            key={d.id}
            onClick={() => handleDemoClick(d.id)}
            style={{
              margin: '0 4px 4px', padding: '3px 7px',
              border: '1px solid #666',
              borderRadius: 2,
              background: selectedDemo === d.id ? '#b0b0b0' : '#ddd',
              fontWeight: selectedDemo === d.id ? 600 : 400,
              color: '#000', cursor: 'pointer',
              fontSize: 12, textAlign: 'left',
            }}
          >
            {d.label}
          </button>
        ))}

        <div style={{ flex: 1 }} />

        {/* Bottom controls */}
        <button onClick={handleResetView} style={{
          margin: '0 4px 4px', padding: '3px 7px',
          border: '1px solid #666', borderRadius: 2,
          background: '#ddd', color: '#000', cursor: 'pointer',
          fontSize: 11,
        }}>
          Zoom Extents
        </button>
        <div style={{ padding: '2px 8px', fontSize: 11, color: '#333', fontFamily: 'monospace' }}>
          {fps} FPS
        </div>
      </div>

      {/* Main viewport */}
      <div style={{ flex: 1, position: 'relative', background: '#000' }}>
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

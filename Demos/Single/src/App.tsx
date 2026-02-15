import { useEffect, useRef, useState, useCallback } from 'react';
import { NoriRenderer } from '@nori/renderer';

const DEMOS = [
  { id: 'dwg', label: 'Drawing Entities' },
  { id: 'linefont', label: 'Line Font' },
  { id: 'convexhull', label: 'Convex Hull' },
  { id: 'boolean', label: 'Polygon Boolean' },
  { id: 'leaf', label: 'Leaf Fill' },
  { id: 'mesh', label: '3D Mesh' },
  { id: 'tess', label: 'Tessellation' },
  { id: 'mes', label: 'Min Enclosing Sphere' },
  { id: 'aabbtree', label: 'AABB Tree' },
  { id: 't3x', label: 'T3X Viewer' },
  { id: 'stp', label: 'STEP Viewer' },
  { id: 'obb', label: 'OBB Builder' },
  { id: 'meshslice', label: 'Mesh Slicing' },
  { id: 'robot', label: 'Robot' },
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
  const [selectedDemo, setSelectedDemo] = useState('dwg');

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

    renderer.onConnected = () => setConnected(true);
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

  const handleResetView = useCallback(() => {
    rendererRef.current?.resetView();
  }, []);

  const handleDemoChange = useCallback((e: React.ChangeEvent<HTMLSelectElement>) => {
    const demoId = e.target.value;
    setSelectedDemo(demoId);
    rendererRef.current?.sendCommand('demo', demoId);
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

        <select value={selectedDemo} onChange={handleDemoChange} style={selectStyle}>
          {DEMOS.map(d => <option key={d.id} value={d.id}>{d.label}</option>)}
        </select>

        <div style={{ flex: 1 }} />

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

const selectStyle: React.CSSProperties = {
  padding: '4px 8px', border: '1px solid #555', borderRadius: 4,
  background: '#2a2a3e', color: '#ccc', cursor: 'pointer',
  fontSize: 12, marginLeft: 8,
};

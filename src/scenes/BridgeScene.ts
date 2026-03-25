import { BaseScene } from './BaseScene';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

export class BridgeScene extends BaseScene {
    private animationId: number = 0;
    private renderer!: THREE.WebGLRenderer;
    private scene!: THREE.Scene;
    private camera!: THREE.PerspectiveCamera;

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
            <style>
                .bridge-container { position:relative; width:100vw; height:100vh; overflow:hidden; background:#2a2f3a; }
                #bridge-canvas { display:block; width:100%; height:100%; }
                .bridge-ui { position:absolute; top:20px; left:20px; z-index:10; color:white; font-family:'Tahoma',sans-serif; text-shadow:1px 1px 2px black; }
                .bridge-ui h2 { margin: 0 0 10px 0; }
                .bridge-ui p { margin: 0 0 20px 0; opacity: 0.8; font-size: 14px; }
                .bridge-back-btn { background: rgba(0,0,0,0.6); border: 1px solid #aaa; color: #fff; padding: 10px 20px; cursor: pointer; border-radius: 5px; font-size: 14px; }
                .bridge-back-btn:hover { background: rgba(255,255,255,0.2); }
                .bridge-loading { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; background:#111; color:#fff; z-index:5; font-family:sans-serif; transition:opacity 0.5s; }
            </style>
            <div class="bridge-container">
                <div class="bridge-loading" id="loading-screen">Загрузка старого моста (3D)...</div>
                <div class="bridge-ui">
                    <h2>Старый мост</h2>
                    <p>Дорога в Казань. Крутите камеру мышкой.</p>
                    <button class="bridge-back-btn" id="b-back-btn">Вернуться в деревню</button>
                </div>
                <canvas id="bridge-canvas"></canvas>
            </div>
        `;

        container.querySelector('#b-back-btn')!.addEventListener('click', () => {
            this.game.scenes.switchScene('village');
        });

        this.initThreeJS();
    }

    private initThreeJS() {
        const canvas = this.container!.querySelector('#bridge-canvas') as HTMLCanvasElement;
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.0;

        this.scene = new THREE.Scene();

        // Fog and background
        this.scene.background = new THREE.Color('#384555');
        this.scene.fog = new THREE.Fog('#384555', 10, 100);

        this.camera = new THREE.PerspectiveCamera(50, window.innerWidth / window.innerHeight, 0.1, 1000);
        this.camera.position.set(20, 15, 20);

        const controls = new OrbitControls(this.camera, this.renderer.domElement);
        controls.enableDamping = true;
        controls.target.set(0, 5, 0);

        // Environment Lights
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.5);
        this.scene.add(ambientLight);

        const dirLight = new THREE.DirectionalLight(0xffeedd, 1.2);
        dirLight.position.set(20, 40, 20);
        this.scene.add(dirLight);

        // Load Bridge
        const loader = new GLTFLoader();
        loader.load('/assets/bridge.glb', (gltf) => {
            const model = gltf.scene;
            
            // Re-center horizontally and shift completely to fit nicely
            const box = new THREE.Box3().setFromObject(model);
            const center = box.getCenter(new THREE.Vector3());
            const size = box.getSize(new THREE.Vector3());
            
            // Adjust scale
            const maxDim = Math.max(size.x, size.y, size.z);
            const scale = 25 / maxDim; // Adjust so it fits in camera nicely
            model.scale.set(scale, scale, scale);

            // Re-calc after scale
            box.setFromObject(model);
            box.getCenter(center);
            model.position.sub(center);
            model.position.y += (box.getSize(new THREE.Vector3()).y / 2);

            this.scene.add(model);
            
            const loadingScreen = this.container!.querySelector('#loading-screen') as HTMLElement;
            if (loadingScreen) {
                loadingScreen.style.opacity = '0';
                setTimeout(() => loadingScreen.remove(), 500);
            }
        }, undefined, (error) => {
            console.error('Error loading bridge model:', error);
            const loadingScreen = this.container!.querySelector('#loading-screen') as HTMLElement;
            if (loadingScreen) loadingScreen.innerText = 'Ошибка загрузки модели моста';
        });

        // Resize handler
        const onWindowResize = () => {
            this.camera.aspect = window.innerWidth / window.innerHeight;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(window.innerWidth, window.innerHeight);
        };
        window.addEventListener('resize', onWindowResize);

        const animate = () => {
            this.animationId = requestAnimationFrame(animate);
            controls.update();
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    }

    destroy() {
        super.destroy();
        if (this.animationId) cancelAnimationFrame(this.animationId);
        if (this.renderer) this.renderer.dispose();
    }
}

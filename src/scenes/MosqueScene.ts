import { BaseScene } from './BaseScene';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

export class MosqueScene extends BaseScene {
    private animationId: number = 0;
    private renderer!: THREE.WebGLRenderer;
    private scene!: THREE.Scene;
    private camera!: THREE.PerspectiveCamera;

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
            <style>
                .mosque-container { position:relative; width:100vw; height:100vh; overflow:hidden; background:#000; }
                #mosque-canvas { display:block; width:100%; height:100%; }
                .mosque-ui { position:absolute; top:20px; left:20px; z-index:10; color:white; font-family:'Tahoma',sans-serif; text-shadow:1px 1px 2px black; }
                .mosque-ui h2 { margin: 0 0 10px 0; }
                .mosque-ui p { margin: 0 0 20px 0; opacity: 0.8; font-size: 14px; }
                .mosque-back-btn { background: rgba(0,0,0,0.6); border: 1px solid #aaa; color: #fff; padding: 10px 20px; cursor: pointer; border-radius: 5px; font-size: 14px; }
                .mosque-back-btn:hover { background: rgba(255,255,255,0.2); }
                .mosque-loading { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; background:#111; color:#fff; z-index:5; font-family:sans-serif; transition:opacity 0.5s; }
            </style>
            <div class="mosque-container">
                <div class="mosque-loading" id="loading-screen">Загрузка 메чети (3D)...</div>
                <div class="mosque-ui">
                    <h2>Мечеть</h2>
                    <p>Тяните мышкой, чтобы осмотреться (OrbitControls).</p>
                    <button class="mosque-back-btn" id="m-back-btn">Выйти на улицу</button>
                </div>
                <canvas id="mosque-canvas"></canvas>
            </div>
        `;

        container.querySelector('#m-back-btn')!.addEventListener('click', () => {
            this.game.scenes.switchScene('village');
        });

        this.initThreeJS();
    }

    private initThreeJS() {
        const canvas = this.container!.querySelector('#mosque-canvas') as HTMLCanvasElement;
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.0;

        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color('#4fb1d3'); // Sky color

        this.camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 1000);
        this.camera.position.set(0, 5, 20);

        const controls = new OrbitControls(this.camera, this.renderer.domElement);
        controls.enableDamping = true;
        controls.target.set(0, 2, 0);

        // Lighting
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        this.scene.add(ambientLight);

        const dirLight = new THREE.DirectionalLight(0xffffee, 1.5);
        dirLight.position.set(10, 20, 10);
        this.scene.add(dirLight);

        // Load Model
        const loader = new GLTFLoader();
        loader.load('/assets/al-aqsa_mosque.glb', (gltf) => {
            const model = gltf.scene;
            
            // Center and scale model depending on its original size
            const box = new THREE.Box3().setFromObject(model);
            const center = box.getCenter(new THREE.Vector3());
            const size = box.getSize(new THREE.Vector3());
            const maxDim = Math.max(size.x, size.y, size.z);
            
            // Scale to reasonable size (e.g., max dimension = 15)
            const scale = 15 / maxDim;
            model.scale.set(scale, scale, scale);
            
            // Re-center
            box.setFromObject(model);
            box.getCenter(center);
            model.position.sub(center);
            // Put it on the ground
            model.position.y += (box.getSize(new THREE.Vector3()).y / 2);

            this.scene.add(model);
            
            const loadingScreen = this.container!.querySelector('#loading-screen') as HTMLElement;
            if (loadingScreen) {
                loadingScreen.style.opacity = '0';
                setTimeout(() => loadingScreen.remove(), 500);
            }
        }, undefined, (error) => {
            console.error('An error happened loading the mosque model', error);
            const loadingScreen = this.container!.querySelector('#loading-screen') as HTMLElement;
            if (loadingScreen) loadingScreen.innerText = 'Ошибка загрузки модели';
        });

        // Resize handler
        const onWindowResize = () => {
            this.camera.aspect = window.innerWidth / window.innerHeight;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(window.innerWidth, window.innerHeight);
        };
        window.addEventListener('resize', onWindowResize);

        // Animation Loop
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
        // NOTE: Memory cleanup for Three.js is deeper in prod, but this works for prototype.
    }
}

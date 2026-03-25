import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

export class GLBToSprite {
    private static renderer: THREE.WebGLRenderer;

    public static async loadIsometricSprite(url: string, scaleMod: number = 1, yOffset: number = 0): Promise<HTMLImageElement> {
        if (!this.renderer) {
            this.renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true, preserveDrawingBuffer: true });
            this.renderer.setSize(512, 512); // High res texture
            this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
            this.renderer.toneMappingExposure = 1.2;
        }

        return new Promise((resolve, reject) => {
            const loader = new GLTFLoader();
            loader.load(url, (gltf) => {
                const scene = new THREE.Scene();
                
                // Lights
                const ambient = new THREE.AmbientLight(0xffffff, 0.7);
                scene.add(ambient);
                const dirLight = new THREE.DirectionalLight(0xffeedd, 1.5);
                dirLight.position.set(20, 40, 20);
                scene.add(dirLight);

                const model = gltf.scene;
                
                // Align to 0,0,0
                const box = new THREE.Box3().setFromObject(model);
                const size = box.getSize(new THREE.Vector3());
                const center = box.getCenter(new THREE.Vector3());
                model.position.sub(center);
                model.position.y += (size.y / 2) + yOffset;

                // Scale into a normalized size (roughly 1 unit = 1 tile width)
                const maxDim = Math.max(size.x, size.z);
                const scale = (3 / maxDim) * scaleMod; // approx fits 3 chunks
                model.scale.set(scale, scale, scale);

                // Rotate model to match standard orientation if needed
                // model.rotation.y = Math.PI / 4; 
                scene.add(model);

                // Isometric Orthographic Camera
                // The isometric angle is roughly exactly the corner of a cube.
                const viewSize = 10;
                const aspect = 1;
                const camera = new THREE.OrthographicCamera(
                    -viewSize * aspect / 2, viewSize * aspect / 2,
                    viewSize / 2, -viewSize / 2,
                    -100, 100
                );
                
                // Position to match the 2D isometric projection
                camera.position.set(10, 10, 10);
                camera.lookAt(0, 0, 0);

                this.renderer.setClearColor(0x000000, 0);
                this.renderer.render(scene, camera);

                const img = new Image();
                img.onload = () => resolve(img);
                img.onerror = reject;
                img.src = this.renderer.domElement.toDataURL('image/png');
                
            }, undefined, reject);
        });
    }
}

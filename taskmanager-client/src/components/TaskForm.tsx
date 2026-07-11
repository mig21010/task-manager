import {useState} from 'react'

interface Props {
    onTaskCreated: () => void;
}

export default function TaskForm({ onTaskCreated }: Props) {
    const [title, setTitle] = useState('');
    const [description, setDescription] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        // Simulate an API call
        try {
            const response = await fetch('https://localhost:7159/api/Tasks', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ title, description, isCompleted: false })
            });
            if (!response.ok) {
                throw new Error('Failed to create task');
            }
            setTitle('');
            setDescription('');
            onTaskCreated();
        } catch (error) {
            console.error('Error creating task:', error);
        } finally {
            setLoading(false);
        }
    };

    return (
       <form onSubmit={handleSubmit} className="bg-white rounded-lg shadow p-4 mb-6">
        <h2 className="text-lg font-semibold text-gray-700 mb-3">
            Nueva Tarea
        </h2>
        <input
            type="text"
            placeholder="Título"
            value={title}
            onChange={e => setTitle(e.target.value)}
            className="w-full border rounded p-2 mb-2 text-sm"
            required
        />
        <input
            type="text"
            placeholder="Descripción"
            value={description}
            onChange={e => setDescription(e.target.value)}
            className="w-full border rounded p-2 mb-3 text-sm"
        />
        <button
            type="submit"
            disabled={loading}
            className="bg-blue-600 text-white px-4 py-2 rounded text-sm hover:bg-blue-700 disabled:opacity-50"
        >
            {loading ? 'Guardando...' : 'Agregar Tarea'}
        </button>
        </form>
    );
}